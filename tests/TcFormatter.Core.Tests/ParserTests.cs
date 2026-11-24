using TcFormatter.Formatting;
using TcFormatter.Syntax;

namespace TcFormatter.Core.Tests;

public class ParserTests
{
    [Fact]
    public void Valid_implementation_has_no_syntax_errors()
    {
        var errors = StSyntax.Parse("IF bStart THEN\n\tnCount := nCount + 1;\nEND_IF\n", CodePart.Implementation);

        Assert.Empty(errors);
    }

    [Theory]
    [InlineData("if bStart then nCount := nCount + 1; end_if")]                       // keywords in any case
    [InlineData("IF a THEN x := 1; ELSIF b THEN x := 2; ELSE x := 3; END_IF;")]
    [InlineData("CASE eState OF\n E_State.Idle, E_State.Init: x := 1;\n 1..5, -7, 16#FF: y := 2; z := 3;\n ELSE x := 0;\nEND_CASE")]
    [InlineData("CASE n OF 1: CASE m OF 2: x := 1; END_CASE END_CASE")]
    [InlineData("FOR i := 1 TO 10 BY 2 DO a[i] := i; END_FOR")]
    [InlineData("WHILE n < 10 DO n := n + 1; IF n = 5 THEN EXIT; END_IF CONTINUE; END_WHILE")]
    [InlineData("REPEAT n := n + 1; UNTIL n >= 10 END_REPEAT")]
    [InlineData("__TRY x := 1; __CATCH(exc) y := 2; __FINALLY z := 3; __ENDTRY")]
    [InlineData("JMP lbl_End; x := 1; lbl_End: RETURN;")]
    [InlineData("bOut S= bSet; bOut R= bReset; refX REF= nY; ipItf ?= ipBase;")]
    [InlineData("fbTimer(IN := bStart, PT := T#5S, Q => bDone, ET => );")]
    [InlineData("fbX(nA := , nB := 1,\n);")]
    [InlineData("fbAxis.M_Move(lrPos := 10.0).bDone; THIS^.nCount := SUPER^.nCount; p^.a[1, 2].b := 3;")]
    [InlineData("n := -a ** 2 + b MOD 3 - NOT c AND d OR e XOR f AND_THEN g OR_ELSE h & i;")]
    [InlineData("b := a <> 5 AND a <= 6 AND a >= 1 AND x = y;")]
    [InlineData("t := T#1S500MS; d := D#2024-01-31; dt := DT#2024-01-01-12:00:00; tod := TOD#12:30:15.5; l := LTIME#1NS;")]
    [InlineData("n := INT#-5; r := REAL#1.5E-3; w := 16#DEAD_BEEF + 2#1010_1010 + 8#17 + 1_000; b := BOOL#TRUE;")]
    [InlineData("e := E_State#Idle; s := 'it$'s'; ws := \"wide\"; nBit := nWord.3;")]
    [InlineData("p := __NEW(FB_X(nA := 1)); __DELETE(p); n := SIZEOF(INT); t := TIME(); x := __SYSTEM.TYPE_CLASS.TYPE_INT;")]
    [InlineData("st := (nA := 1, stB := (nC := 2)); a := [1, 2, 3];")]
    [InlineData("{region 'r'} x := 1; {endregion} (* c (* nested *) *) // line\n /* c */ ;;")]
    [InlineData("")]
    public void Implementation_constructs_parse(string code)
    {
        Assert.Empty(StSyntax.Parse(code, CodePart.Implementation));
    }

    [Theory]
    [InlineData("FUNCTION_BLOCK PUBLIC ABSTRACT FB_Axis EXTENDS Base.FB_Base IMPLEMENTS I_A, I_B\nVAR_INPUT\n\tbExecute : BOOL;\nEND_VAR")]
    [InlineData("PROGRAM MAIN\nVAR\n\tfbMotor : FB_Motor(nId := 1) := (bEnable := TRUE);\nEND_VAR")]
    [InlineData("FUNCTION F_Scale : LREAL\nVAR_INPUT\n\tlrIn : LREAL;\nEND_VAR\nVAR CONSTANT\n\tcMax : INT := 10;\nEND_VAR")]
    [InlineData("METHOD PROTECTED FINAL M_Do : BOOL\nVAR_IN_OUT\n\taData : ARRAY[*] OF INT;\nEND_VAR\nVAR_TEMP\n\ti : DINT;\nEND_VAR")]
    [InlineData("PROPERTY PUBLIC nValue : INT")]
    [InlineData("PUBLIC\nVAR\nEND_VAR")]
    [InlineData("INTERFACE I_Motor EXTENDS I_Base, I_Other")]
    [InlineData("ACTION A_Reset:")]
    [InlineData("{attribute 'qualified_only'}\nVAR_GLOBAL CONSTANT RETAIN\n\tnA, nB : INT := 16#FF;\n\tbIn AT %IX0.1 : BOOL;\n\tnOut AT %Q* : WORD;\nEND_VAR\nVAR_GLOBAL PERSISTENT\nEND_VAR")]
    [InlineData("VAR\n\tp : POINTER TO POINTER TO INT;\n\tr : REFERENCE TO FB_X REF= fbX;\n\ts : STRING(80) := 'x';\n\tws : WSTRING[cLen];\n\tn : INT(0..100) := 5;\n\ta : ARRAY[1..2, 0..cMax] OF ST_X := [(nA := 1), (nA := 2)];\n\tz : ARRAY[0..9] OF INT := [10(0)];\n\te : (Off, On) := Off;\nEND_VAR")]
    [InlineData("TYPE E_State :\n(\n\tIdle := 0,\n\tRun\n) UDINT := Idle;\nEND_TYPE")]
    [InlineData("TYPE ST_Data EXTENDS ST_Base :\nSTRUCT\n\tnA : INT;\nEND_STRUCT\nEND_TYPE")]
    [InlineData("TYPE U_Value :\nUNION\n\tnA : INT;\n\tlr : LREAL;\nEND_UNION\nEND_TYPE")]
    [InlineData("TYPE T_Name : STRING(80); END_TYPE")]
    public void Declaration_constructs_parse(string code)
    {
        Assert.Empty(StSyntax.Parse(code, CodePart.Declaration));
    }

    [Theory]
    [InlineData("IF a THEN x := 1;", CodePart.Implementation)]                          // missing END_IF
    [InlineData("x := 1", CodePart.Implementation)]                                     // missing ';'
    [InlineData("x := (1 + 2;", CodePart.Implementation)]
    [InlineData("x := 1 ? 2;", CodePart.Implementation)]                                // unknown character
    [InlineData("CASE n OF 1: x := 1;", CodePart.Implementation)]
    [InlineData("x := 1; END_VAR", CodePart.Implementation)]
    [InlineData("VAR\n\tnA : INT\nEND_VAR", CodePart.Declaration)]                      // missing ';'
    [InlineData("VAR\n\tnA : INT;", CodePart.Declaration)]                              // missing END_VAR
    [InlineData("TYPE ST_X : STRUCT nA : INT; END_STRUCT", CodePart.Declaration)]       // missing END_TYPE
    public void Invalid_code_reports_a_syntax_error(string code, CodePart part)
    {
        Assert.Single(StSyntax.Parse(code, part));
    }

    [Fact]
    public void Syntax_error_reports_line_and_column()
    {
        var errors = StSyntax.Parse("nA := 1;\nIF bStart nCount := 1; END_IF", CodePart.Implementation);

        var error = Assert.Single(errors);
        Assert.Equal(2, error.Line);
        Assert.Equal(11, error.Column);
    }
}
