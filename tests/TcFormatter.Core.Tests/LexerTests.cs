using TcFormatter.Lexing;

namespace TcFormatter.Core.Tests;

public class LexerTests
{
    [Theory]
    [InlineData("")]
    [InlineData("a:=b+1;")]
    [InlineData("IF bStart THEN\r\n\tnCount := nCount + 1;\r\nEND_IF\r\n")]
    [InlineData("sText := 'it$'s $N ok'; wsText := \"wide $\" text\";")]
    [InlineData("(* outer (* nested *) still comment *) x := 1; // line\n/* c-style */")]
    [InlineData("{attribute 'qualified_only'}\nVAR_GLOBAL\n  nVal : INT := 16#FF;\nEND_VAR")]
    [InlineData("tWait := T#1S500MS; dtNow := DT#2024-01-01-12:00:00; f := 1.5E-3; n := INT#-5; r := 2#1010_1010;")]
    [InlineData("aData[1..10] := x; p^ := 3; fb(nIn := 1, bOut => bRes);")]
    [InlineData("unterminated 'string\n and (* comment")]
    public void Tokenize_then_concatenate_reproduces_the_input_exactly(string source)
    {
        var tokens = Lexer.Tokenize(source);

        Assert.Equal(source, string.Concat(tokens.Select(t => t.Text)));
    }

    [Fact]
    public void Classifies_keywords_identifiers_literals_comments_and_pragmas()
    {
        var tokens = Lexer.Tokenize("IF bX THEN // c\n{warning 'w'} n := 16#FF; END_IF")
            .Where(t => !t.IsTrivia)
            .Select(t => (t.Kind, t.Text))
            .ToList();

        Assert.Equal(new (TokenKind, string)[]
        {
            (TokenKind.Keyword, "IF"),
            (TokenKind.Identifier, "bX"),
            (TokenKind.Keyword, "THEN"),
            (TokenKind.LineComment, "// c"),
            (TokenKind.Pragma, "{warning 'w'}"),
            (TokenKind.Identifier, "n"),
            (TokenKind.Operator, ":="),
            (TokenKind.Number, "16#FF"),
            (TokenKind.Operator, ";"),
            (TokenKind.Keyword, "END_IF"),
        }, tokens);
    }

    [Theory]
    [InlineData("T#1s", TokenKind.TimeLiteral)]
    [InlineData("TIME#5m3s", TokenKind.TimeLiteral)]
    [InlineData("LTIME#1ns", TokenKind.TimeLiteral)]
    [InlineData("D#2024-01-31", TokenKind.TimeLiteral)]
    [InlineData("TOD#12:30:15.5", TokenKind.TimeLiteral)]
    [InlineData("DT#2024-01-01-12:00:00", TokenKind.TimeLiteral)]
    [InlineData("16#DEAD_BEEF", TokenKind.Number)]
    [InlineData("1.5E-3", TokenKind.Number)]
    [InlineData("INT#16#7F", TokenKind.Number)]
    [InlineData("E_State.Idle", TokenKind.Identifier)]
    [InlineData("%I*", TokenKind.DirectAddress)]
    [InlineData("%QX0.1", TokenKind.DirectAddress)]
    [InlineData("%MW10", TokenKind.DirectAddress)]
    [InlineData("%IB*", TokenKind.DirectAddress)]
    public void Recognises_single_literal(string source, TokenKind expected)
    {
        var first = Lexer.Tokenize(source).First();

        Assert.Equal(expected, first.Kind);
        if (expected != TokenKind.Identifier)
            Assert.Equal(source, first.Text);
    }
}
