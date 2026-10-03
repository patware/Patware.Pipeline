using System.Globalization;
using System.Text;

namespace Pipeline.Blazor.Logging;

/// <summary>
/// Pairs plain log text with the inline CSS derived from its ANSI formatting state.
/// </summary>
/// <param name="Text">The plain segment text, with terminal control sequences removed; render as HTML-encoded text.</param>
/// <param name="Css">The inline CSS for the segment; an empty string inherits the surrounding log style.</param>
public sealed record AnsiLogSegment(string Text, string Css);

/// <summary>
/// Converts ANSI SGR formatting to text segments and CSS while discarding unsupported terminal controls.
/// </summary>
public static class AnsiLogParser
{
    private static readonly string[] Palette =
    [
        "#000000", "#cd3131", "#0dbc79", "#e5e510",
        "#2472c8", "#bc3fbc", "#11a8cd", "#e5e5e5",
        "#666666", "#f14c4c", "#23d18b", "#f5f543",
        "#3b8eea", "#d670d6", "#29b8db", "#ffffff"
    ];

    private sealed class Style
    {
        public bool Bold;
        public bool Dim;
        public bool Italic;
        public bool Underline;
        public bool Strike;
        public string? Foreground;
        public string? Background;

        public string ToCss()
        {
            var css = new StringBuilder();

            if (Bold) css.Append("font-weight:700;");
            if (Dim) css.Append("opacity:0.7;");
            if (Italic) css.Append("font-style:italic;");

            if (Underline || Strike)
            {
                css.Append("text-decoration-line:");
                if (Underline) css.Append("underline ");
                if (Strike) css.Append("line-through ");
                css.Append(';');
            }

            if (Foreground is not null)
                css.Append("color:").Append(Foreground).Append(';');

            if (Background is not null)
                css.Append("background-color:").Append(Background).Append(';');

            return css.ToString();
        }
    }

    /// <summary>
    /// Splits text into ANSI-styled segments, normalizes carriage returns, and removes unsupported or malformed control sequences.
    /// </summary>
    /// <param name="text">The log text containing optional ANSI formatting sequences.</param>
    /// <returns>Plain text segments with CSS; formatting starts fresh on each call.</returns>
    public static IReadOnlyList<AnsiLogSegment> Parse(string text)
    {
        var segments = new List<AnsiLogSegment>();
        var buffer = new StringBuilder();
        var style = new Style();

        void Flush()
        {
            if (buffer.Length == 0)
                return;

            segments.Add(new AnsiLogSegment(
                buffer.ToString(),
                style.ToCss()));

            buffer.Clear();
        }

        for (var i = 0; i < text.Length;)
        {
            var character = text[i++];

            if (character == '\u001b')
            {
                Flush();

                if (i >= text.Length)
                    break;

                var introducer = text[i++];

                if (introducer == '[')
                {
                    var start = i;

                    // CSI ends with a character in the range @ through ~.
                    while (i < text.Length &&
                           !(text[i] >= '@' && text[i] <= '~'))
                    {
                        i++;
                    }

                    if (i >= text.Length)
                        break;

                    if (text[i] == 'm')
                        ApplySgr(text[start..i], ref style);

                    i++;
                }
                else if (introducer is ']' or 'P' or 'X' or '^' or '_')
                {
                    // Discard OSC/DCS/control strings, including hyperlinks.
                    // OSC can end in BEL; all can end in ESC followed by \.
                    while (i < text.Length)
                    {
                        if (introducer == ']' && text[i] == '\a')
                        {
                            i++;
                            break;
                        }

                        if (text[i] == '\u001b' &&
                            i + 1 < text.Length &&
                            text[i + 1] == '\\')
                        {
                            i += 2;
                            break;
                        }

                        i++;
                    }
                }
                else
                {
                    // Consume any intermediate bytes of another ESC command.
                    while (introducer >= ' ' &&
                           introducer <= '/' &&
                           i < text.Length)
                    {
                        introducer = text[i++];
                    }
                }

                continue;
            }

            if (character == '\r')
            {
                // Preserve line structure without terminal cursor rewriting.
                if (i < text.Length && text[i] == '\n')
                    i++;

                buffer.Append('\n');
            }
            else if (character is '\n' or '\t' ||
                     !char.IsControl(character))
            {
                buffer.Append(character);
            }
        }

        Flush();
        return segments;
    }

    private static void ApplySgr(string parameters, ref Style style)
    {
        var parts = parameters.Split(';');
        var codes = new int[parts.Length];

        for (var i = 0; i < parts.Length; i++)
        {
            // An omitted SGR parameter means reset.
            if (parts[i].Length == 0)
            {
                codes[i] = 0;
            }
            else if (!int.TryParse(
                parts[i],
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out codes[i]))
            {
                return;
            }
        }

        for (var i = 0; i < codes.Length; i++)
        {
            var code = codes[i];

            switch (code)
            {
                case 0:
                    style = new Style();
                    break;
                case 1:
                    style.Bold = true;
                    break;
                case 2:
                    style.Dim = true;
                    break;
                case 3:
                    style.Italic = true;
                    break;
                case 4:
                    style.Underline = true;
                    break;
                case 9:
                    style.Strike = true;
                    break;
                case 22:
                    style.Bold = false;
                    style.Dim = false;
                    break;
                case 23:
                    style.Italic = false;
                    break;
                case 24:
                    style.Underline = false;
                    break;
                case 29:
                    style.Strike = false;
                    break;
                case 39:
                    style.Foreground = null;
                    break;
                case 49:
                    style.Background = null;
                    break;
                case >= 30 and <= 37:
                    style.Foreground = Palette[code - 30];
                    break;
                case >= 40 and <= 47:
                    style.Background = Palette[code - 40];
                    break;
                case >= 90 and <= 97:
                    style.Foreground = Palette[code - 90 + 8];
                    break;
                case >= 100 and <= 107:
                    style.Background = Palette[code - 100 + 8];
                    break;
                case 38:
                case 48:
                    if (!TryReadColor(codes, ref i, out var color))
                        return;

                    if (code == 38)
                        style.Foreground = color;
                    else
                        style.Background = color;

                    break;
            }
        }
    }

    private static bool TryReadColor(
        int[] codes,
        ref int index,
        out string color)
    {
        color = "";

        if (index + 1 >= codes.Length)
            return false;

        var mode = codes[++index];

        if (mode == 5)
        {
            if (index + 1 >= codes.Length)
                return false;

            var value = codes[++index];

            if (value is < 0 or > 255)
                return false;

            color = IndexedColor(value);
            return true;
        }

        if (mode == 2)
        {
            if (index + 3 >= codes.Length)
                return false;

            var red = codes[++index];
            var green = codes[++index];
            var blue = codes[++index];

            if (red is < 0 or > 255 ||
                green is < 0 or > 255 ||
                blue is < 0 or > 255)
            {
                return false;
            }

            color = $"#{red:X2}{green:X2}{blue:X2}";
            return true;
        }

        return false;
    }

    private static string IndexedColor(int index)
    {
        if (index < 16)
            return Palette[index];

        if (index >= 232)
        {
            var gray = 8 + (index - 232) * 10;
            return $"#{gray:X2}{gray:X2}{gray:X2}";
        }

        var value = index - 16;

        static int Channel(int component) => component == 0 ? 0 : 55 + component * 40;

        var red = Channel(value / 36);
        var green = Channel(value / 6 % 6);
        var blue = Channel(value % 6);

        return $"#{red:X2}{green:X2}{blue:X2}";
    }
}