using Pipeline.Blazor.Logging;

namespace Pipeline.Blazor.Tests;

[TestClass]
public class AnsiLogParserTests
{
    [TestMethod]
    [DataRow("", "")]
    [DataRow("plain", "plain")]
    [DataRow("a\r\nb\rc\n\td", "a\nb\nc\n\td")]
    [DataRow("a\0\a\bb", "ab")]
    [DataRow("a\u001b[2Jb", "ab")]
    [DataRow("a\u001b]8;;https://example.com\ab\u001b]8;;\a", "ab")]
    [DataRow("a\u001bPsecret\u001b\\b", "ab")]
    [DataRow("a\u001b[31", "a")]
    [DataRow("a\u001b", "a")]
    [DataRow("a\u001b(Bb", "ab")]
    public void Parser_preserves_text_and_strips_terminal_control_sequences(string input, string expected)
        => string.Concat(AnsiLogParser.Parse(input).Select(x => x.Text)).Should().Be(expected);

    [TestMethod]
    [DataRow("1", "font-weight:700;")]
    [DataRow("2", "opacity:0.7;")]
    [DataRow("3", "font-style:italic;")]
    [DataRow("4", "text-decoration-line:underline ;")]
    [DataRow("9", "text-decoration-line:line-through ;")]
    [DataRow("31", "color:#cd3131;")]
    [DataRow("44", "background-color:#2472c8;")]
    [DataRow("97", "color:#ffffff;")]
    [DataRow("100", "background-color:#666666;")]
    [DataRow("38;5;1", "color:#cd3131;")]
    [DataRow("38;5;16", "color:#000000;")]
    [DataRow("38;5;21", "color:#0000FF;")]
    [DataRow("38;5;231", "color:#FFFFFF;")]
    [DataRow("38;5;232", "color:#080808;")]
    [DataRow("38;5;255", "color:#EEEEEE;")]
    [DataRow("48;2;1;128;255", "background-color:#0180FF;")]
    public void Sgr_produces_expected_css(string sgr, string css)
        => AnsiLogParser.Parse($"\u001b[{sgr}mtext").Should().Equal(new AnsiLogSegment("text", css));

    [TestMethod]
    [DataRow("0")] [DataRow("")] [DataRow("22;23;24;29;39;49")]
    public void Reset_clears_accumulated_styles(string reset)
    {
        var segments = AnsiLogParser.Parse($"\u001b[1;2;3;4;9;31;44mbefore\u001b[{reset}mafter");
        segments.Should().HaveCount(2);
        segments[0].Css.Should().Contain("font-weight:700;").And.Contain("line-through").And.Contain("color:");
        segments[1].Should().Be(new AnsiLogSegment("after", ""));
    }

    [TestMethod]
    [DataRow("38")] [DataRow("38;5")] [DataRow("38;5;256")]
    [DataRow("38;2;1;2")] [DataRow("38;2;256;0;0")] [DataRow("38;9;1")]
    [DataRow("-1")] [DataRow("999999999999999999999")] [DataRow("999")]
    public void Unsupported_or_malformed_styles_do_not_leak_into_text(string sgr)
        => AnsiLogParser.Parse($"\u001b[{sgr}mtext").Should().Equal(new AnsiLogSegment("text", ""));
}
