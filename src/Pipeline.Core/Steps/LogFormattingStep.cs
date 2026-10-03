namespace Pipeline.Core.Steps;

/// <summary>
/// Writes severity and ANSI formatting samples to exercise the pipeline log viewer.
/// </summary>
/// <param name="logger">The logger receiving diagnostic or invocation messages.</param>
public sealed class LogFormattingStep(IPipelineStepLogger logger)
{
    private const string Escape = "\u001b[";
    private const string Reset = "\u001b[0m";

    /// <summary>
    /// Writes the formatting demonstration entries; error-level samples do not fail execution.
    /// </summary>
    /// <param name="cancellationToken">The token used to cancel this operation.</param>
    /// <returns>A task that completes after all sample entries have been written.</returns>
    public async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        Task Info(string message) => logger.InformationAsync(message, cancellationToken);

        Task Warn(string message) => logger.WarningAsync(message, cancellationToken);

        Task Error(string message) => logger.ErrorAsync(message, cancellationToken);

        await Info("=== Log formatting samples ===");

        // Severity defaults.
        await Info("INFO: normal informational message.");
        await Warn("WARN: normal warning message.");
        await Error("ERROR: sample error message; this step still succeeds.");

        // Individual styles and selective resets.
        await Info($"Bold: {Escape}1mbold text{Escape}22m normal text.");
        await Info($"Dim: {Escape}2mdim text{Escape}22m normal text.");
        await Info($"Italic: {Escape}3mitalic text{Escape}23m normal text.");
        await Info($"Underline: {Escape}4munderlined text{Escape}24m normal text.");
        await Info($"Strike: {Escape}9mstruck text{Escape}29m normal text.");

        // Combined styles, followed by selective and complete resets.
        await Info(
            $"Combined: {Escape}1;3;4mbold, italic, underlined" +
            $"{Escape}24m bold and italic only" +
            $"{Escape}23m bold only" +
            $"{Reset} normal.");

        await Info(
            $"Intensity reset: {Escape}1;2mbold and dim" +
            $"{Escape}22m normal.");

        // Standard and bright foreground colors.
        // Give black a light background so its sample remains visible.
        for (var index = 0; index < 8; index++)
        {
            var background = index == 0 ? $"{Escape}47m" : "";

            await Info(
                $"Foreground {30 + index}: " +
                $"{background}{Escape}{30 + index}mstandard sample{Reset} | " +
                $"Foreground {90 + index}: " +
                $"{Escape}{90 + index}mbright sample{Reset}");
        }

        // Standard and bright background colors.
        for (var index = 0; index < 8; index++)
        {
            var foreground = index is 2 or 3 or 6 or 7 ? 30 : 97;

            await Info(
                $"Background {40 + index}: " +
                $"{Escape}{foreground};{40 + index}m standard sample {Reset} | " +
                $"Background {100 + index}: " +
                $"{Escape}30;{100 + index}m bright sample {Reset}");
        }

        // Extended color forms supported by the current parser.
        await Info($"256-color foreground: {Escape}38;5;208morange sample{Reset}.");

        await Info($"256-color background: {Escape}30;48;5;153m blue background {Reset}.");

        await Info($"RGB foreground: {Escape}38;2;120;200;255mcustom blue{Reset}.");

        await Info($"RGB background: {Escape}30;48;2;255;220;150m custom background {Reset}.");

        // Reset must restore severity defaults, not always white.
        await Warn($"Warning reset: {Escape}36mcyan override{Reset} warning color again.");

        await Error($"Error reset: {Escape}32mgreen override{Reset} error color again.");

        await Warn($"Foreground reset: {Escape}35mmagenta{Escape}39m warning color again.");

        await Info(
            $"Background reset: {Escape}48;5;24mblue background" +
            $"{Escape}49m default background.");

        // Whitespace preservation and wrapping.
        await Info(
            "Multiline indentation:\n" +
            "Parent\n" +
            "    Child\n" +
            "        Grandchild");

        await Info("Tabs:\nName\tState\nAlpha\tReady\nBeta\tWaiting");

        await Info(
            $"Style across a newline: {Escape}1mbold first line\n" +
            $"bold second line{Reset}\nnormal third line");

        await Info("CRLF normalization:\r\nSecond line\r\nThird line");

        await Info("Long unbroken value: " + new string('X', 240));

        // Encoding and unsupported-control handling.
        await Info(
            "Literal HTML: <strong>not bold</strong> " +
            "<script>alert('not executed')</script> & \"quotes\"");

        await Info("Unsupported screen-clear control: before\u001b[2J after.");

        await Info(
            "Unsupported hyperlink: " +
            "\u001b]8;;https://example.com\u001b\\" +
            "plain link text" +
            "\u001b]8;;\u001b\\");

        // Malformed input should not crash the viewer.
        await Info("Incomplete escape at end: visible text\u001b[");
        await Info("Invalid extended color: \u001b[38;5;999mvisible text.");

        // Formatting must not leak between separate entries.
        await Info($"Unclosed formatting: {Escape}1;35mbold magenta to end");
        await Info("Fresh entry: normal weight and informational color.");

        await Info(
            $"Style API: {Style.Bold}bold{Style.Reset}, " +
            $"{Style.Italic}italic{Style.Reset}, " +
            $"{Style.Underline}underlined{Style.Reset}.");

        await Info(
            $"Combined: {Style.Bold}{Style.Blue}bold blue{Style.Reset} normal.");

        await Info(
            $"Background: {Style.Black}{Style.BackgroundWhite} readable text {Style.Reset}.");

        await Warn(
            $"Reset: {Style.Green}green override{Style.Reset} warning color again.");

        await Info("=== Formatting samples complete ===");
    }
}