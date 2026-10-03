namespace Pipeline.Core;

/// <summary>
/// ANSI formatting sequences supported by the pipeline log viewer.
/// Styles accumulate until reset.
/// </summary>
public static class Style
{
    /// <summary>
    /// Clears all formatting and restores the log entry's default appearance.
    /// </summary>
    public const string Reset = "\u001b[0m";

    /// <summary>
    /// Enables ANSI bold formatting until changed or reset.
    /// </summary>
    public const string Bold = "\u001b[1m";
    /// <summary>
    /// Enables ANSI italic formatting until changed or reset.
    /// </summary>
    public const string Italic = "\u001b[3m";
    /// <summary>
    /// Enables ANSI underline formatting until changed or reset.
    /// </summary>
    public const string Underline = "\u001b[4m";

    /// <summary>
    /// Sets the ANSI foreground color to red until changed or reset.
    /// </summary>
    public const string Red = "\u001b[31m";
    /// <summary>
    /// Sets the ANSI foreground color to green until changed or reset.
    /// </summary>
    public const string Green = "\u001b[32m";
    /// <summary>
    /// Sets the ANSI foreground color to yellow until changed or reset.
    /// </summary>
    public const string Yellow = "\u001b[33m";
    /// <summary>
    /// Sets the ANSI foreground color to blue until changed or reset.
    /// </summary>
    public const string Blue = "\u001b[34m";
    /// <summary>
    /// Sets the ANSI foreground color to white until changed or reset.
    /// </summary>
    public const string White = "\u001b[37m";
    /// <summary>
    /// Sets the ANSI foreground color to black until changed or reset.
    /// </summary>
    public const string Black = "\u001b[30m";

    /// <summary>
    /// Sets the ANSI background color to red until changed or reset.
    /// </summary>
    public const string BackgroundRed = "\u001b[41m";
    /// <summary>
    /// Sets the ANSI background color to green until changed or reset.
    /// </summary>
    public const string BackgroundGreen = "\u001b[42m";
    /// <summary>
    /// Sets the ANSI background color to yellow until changed or reset.
    /// </summary>
    public const string BackgroundYellow = "\u001b[43m";
    /// <summary>
    /// Sets the ANSI background color to blue until changed or reset.
    /// </summary>
    public const string BackgroundBlue = "\u001b[44m";
    /// <summary>
    /// Sets the ANSI background color to white until changed or reset.
    /// </summary>
    public const string BackgroundWhite = "\u001b[47m";
    /// <summary>
    /// Sets the ANSI background color to black until changed or reset.
    /// </summary>
    public const string BackgroundBlack = "\u001b[40m";
}