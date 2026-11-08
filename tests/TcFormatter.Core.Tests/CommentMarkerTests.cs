using TcFormatter.Formatting;

namespace TcFormatter.Core.Tests;

/// <summary>Header comments whose lines end with a marker like "&lt;br&gt;" at a fixed column (PACB help header).</summary>
public class CommentMarkerTests : FormatterTestBase
{
    private const string Header =
        "(*\n" +
        "Copyright Syntegon.<br>\n" +
        "DESCRIPTION:    \t\t\t\t\t\t\t\t<br>  \n" +
        "<br>\n" +
        "A long description that passes the column<br>\n" +
        "no marker here\n" +
        "*)\n" +
        "FUNCTION_BLOCK FB_X\nVAR\nEND_VAR";

    [Fact]
    public void Markers_are_aligned_with_tabs()
    {
        var lines = Decl(Header, o =>
        {
            o.IndentStyle = IndentStyle.Tabs;
            o.AlignWithTabs = true;
            o.CommentLineEndMarker = "<br>";
            o.CommentLineEndMarkerColumn = 25; // tab stop 6 (column 24 zero based)
        }).Split('\n');

        Assert.Equal("Copyright Syntegon.\t\t<br>", lines[1]);
        Assert.Equal("DESCRIPTION:\t\t\t<br>", lines[2]);
        Assert.Equal("\t\t\t\t\t\t<br>", lines[3]);
        Assert.Equal("A long description that passes the column\t<br>", lines[4]);
        Assert.Equal("no marker here", lines[5]);
    }

    [Fact]
    public void Markers_are_aligned_with_spaces()
    {
        var lines = Decl(Header, o => { o.CommentLineEndMarker = "<br>"; o.CommentLineEndMarkerColumn = 25; }).Split('\n');

        Assert.Equal("Copyright Syntegon.     <br>", lines[1]);
        Assert.Equal("DESCRIPTION:            <br>", lines[2]);
        Assert.Equal(new string(' ', 24) + "<br>", lines[3]);
        Assert.Equal("A long description that passes the column <br>", lines[4]);
    }

    [Fact]
    public void A_line_that_ends_exactly_at_the_column_keeps_the_marker_directly_behind_it()
    {
        var input = "(*\n" + new string('-', 24) + "<br>\n" + new string('-', 24) + "\t<br>\n*)\nFUNCTION_BLOCK FB_X\nVAR\nEND_VAR";
        var lines = Decl(input, o =>
        {
            o.IndentStyle = IndentStyle.Tabs;
            o.AlignWithTabs = true;
            o.CommentLineEndMarker = "<br>";
            o.CommentLineEndMarkerColumn = 25;
        }).Split('\n');

        Assert.Equal(new string('-', 24) + "<br>", lines[1]);
        Assert.Equal(new string('-', 24) + "<br>", lines[2]);
    }

    [Fact]
    public void Comments_are_left_alone_without_a_marker()
    {
        var lines = Decl(Header).Split('\n');
        Assert.Equal("DESCRIPTION:    \t\t\t\t\t\t\t\t<br>  ", lines[2]);
    }
}
