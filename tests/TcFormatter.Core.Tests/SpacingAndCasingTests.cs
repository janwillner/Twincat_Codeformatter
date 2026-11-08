using TcFormatter.Formatting;

namespace TcFormatter.Core.Tests;

public class SpacingAndCasingTests : FormatterTestBase
{
    [Theory]
    [InlineData("x:=a+b*c-d/e;", "x := a + b * c - d / e;")]
    [InlineData("x  :=  ( a+b )*2;", "x := (a + b) * 2;")]
    [InlineData("x:=-1; y:=a*-b; z:=-(a);", "x := -1;\ny := a * -b;\nz := -(a);")]
    [InlineData("b:=a<>b AND c>=d OR NOT e;", "b := a <> b AND c >= d OR NOT e;")]
    [InlineData("n:=2**3 MOD 5;", "n := 2 ** 3 MOD 5;")]
    [InlineData("fbTimer ( IN:=bStart , PT:=T#1S , Q=>bDone );", "fbTimer(IN := bStart, PT := T#1S, Q => bDone);")]
    [InlineData("aData [ i+1 ] := p ^ . nValue;", "aData[i + 1] := p^.nValue;")]
    [InlineData("THIS^.M_Do( ); SUPER^ ( );", "THIS^.M_Do();\nSUPER^();")]
    [InlineData("bSet S= bIn; bRes R= bIn; refX REF= nVal;", "bSet S= bIn;\nbRes R= bIn;\nrefX REF= nVal;")]
    [InlineData("s := CONCAT( 'a' ,'b' );", "s := CONCAT('a', 'b');")]
    [InlineData("x := E_State.Idle; y := E_State#Run;", "x := E_State.Idle;\ny := E_State#Run;")]
    [InlineData("IF(a)THEN b:=1; END_IF", "IF (a) THEN\n    b := 1;\nEND_IF")]
    public void Normalises_spaces_in_statements(string input, string expected)
    {
        Assert.Equal(expected, Impl(input));
    }

    [Theory]
    [InlineData("nA:INT;", "nA : INT;")]
    [InlineData("aData:ARRAY [ 1..10 ] OF INT;", "aData : ARRAY[1..10] OF INT;")]
    [InlineData("sName:STRING ( 80 ) :='x';", "sName : STRING(80) := 'x';")]
    [InlineData("bIn AT %I*:BOOL;", "bIn AT %I* : BOOL;")]
    [InlineData("pData:POINTER TO ST_Data;", "pData : POINTER TO ST_Data;")]
    [InlineData("fbX:FB_X(nCount:=5);", "fbX : FB_X(nCount := 5);")]
    public void Normalises_spaces_in_declarations(string member, string expected)
    {
        Assert.Equal($"VAR\n    {expected}\nEND_VAR", Decl($"VAR\n{member}\nEND_VAR"));
    }

    [Fact]
    public void Spacing_options_can_be_switched()
    {
        Assert.Equal("x:=a+b;", Impl("x := a + b;", o => { o.SpaceAroundAssignment = false; o.SpaceAroundOperators = false; }));
        Assert.Equal("F( a,b );", Impl("F(a, b);", o => { o.SpaceInsideParentheses = true; o.SpaceAfterComma = false; }));
        Assert.Equal("fb (x := 1);", Impl("fb(x := 1);", o => o.SpaceBeforeCallParenthesis = true));
        Assert.Equal("VAR\n    a: ARRAY [1 .. 2] OF INT;\nEND_VAR",
            Decl("VAR\na : ARRAY[1..2] OF INT;\nEND_VAR", o => { o.SpaceBeforeColon = false; o.SpaceAfterArrayKeyword = true; o.SpaceAroundRange = true; }));
    }

    [Fact]
    public void Applies_keyword_type_and_literal_casing()
    {
        Assert.Equal("IF bA AND TRUE THEN\n    n := INT#16#FF + 16#AB;\n    t := T#1s500ms;\nEND_IF",
            Impl("if bA and true then\nn := int#16#ff + 16#ab;\nt := t#1s500ms;\nend_if"));

        Assert.Equal("if bA and true then\n    n := 16#ab;\nend_if",
            Impl("IF bA AND TRUE THEN\nn := 16#AB;\nEND_IF", o => { o.KeywordCasing = CasingStyle.Lower; o.LiteralCasing = CasingStyle.Lower; }));

        Assert.Equal("VAR\n    n : int;\nEND_VAR", Decl("VAR\nn : int;\nEND_VAR", o => o.TypeCasing = CasingStyle.Preserve));
        Assert.Equal("VAR\n    n : INT;\nEND_VAR", Decl("var\nn : int;\nend_var"));

        Assert.Equal("t := T#1S500MS;", Impl("t := t#1s500ms;", o => o.TimeUnitCasing = CasingStyle.Upper));
    }

    [Fact]
    public void Never_changes_identifiers_strings_or_comments_casing()
    {
        Assert.Equal("myVar := 'Hello World'; // Mixed Case", Impl("myVar:='Hello World'; // Mixed Case"));
    }

    [Fact]
    public void Adds_space_after_line_comment_start()
    {
        Assert.Equal(Lines("// comment", "//===", "/// doc", "x := 1; // trailing"),
            Impl(Lines("//comment", "//===", "/// doc", "x := 1; //trailing")));
        Assert.Equal("//comment", Impl("//comment", o => o.SpaceAfterLineCommentStart = false));
    }
}
