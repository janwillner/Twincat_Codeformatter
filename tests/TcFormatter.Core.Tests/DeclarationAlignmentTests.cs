using TcFormatter.Formatting;

namespace TcFormatter.Core.Tests;

public class DeclarationAlignmentTests : FormatterTestBase
{
    [Fact]
    public void Aligns_colons_initializers_and_comments()
    {
        var input = Lines(
            "VAR",
            "bStart:BOOL; // start",
            "nCounterValue : INT := 5;",
            "{attribute 'hide'}",
            "fValue : LREAL := 1.5; (* value *)",
            "sName : STRING;",
            "END_VAR");

        Assert.Equal(Lines(
            "VAR",
            "    bStart        : BOOL;         // start",
            "    nCounterValue : INT   := 5;",
            "    {attribute 'hide'}",
            "    fValue        : LREAL := 1.5; (* value *)",
            "    sName         : STRING;",
            "END_VAR"), Decl(input));
    }

    [Fact]
    public void Blank_lines_start_a_new_alignment_block()
    {
        var input = Lines("VAR", "a : INT;", "bLonger : BOOL;", "", "nX : INT;", "nLongName : BOOL;", "END_VAR");

        Assert.Equal(Lines(
            "VAR",
            "    a       : INT;",
            "    bLonger : BOOL;",
            "",
            "    nX        : INT;",
            "    nLongName : BOOL;",
            "END_VAR"), Decl(input));

        Assert.Equal(Lines(
            "VAR",
            "    a         : INT;",
            "    bLonger   : BOOL;",
            "",
            "    nX        : INT;",
            "    nLongName : BOOL;",
            "END_VAR"), Decl(input, o => o.BlankLineBreaksAlignment = false));
    }

    [Fact]
    public void Alignment_can_be_switched_off()
    {
        var input = Lines("VAR", "a:INT:=1; // c", "bLonger:BOOL;", "END_VAR");
        Assert.Equal(Lines("VAR", "    a : INT := 1; // c", "    bLonger : BOOL;", "END_VAR"),
            Decl(input, o =>
            {
                o.AlignDeclarationColons = false;
                o.AlignDeclarationInitializers = false;
                o.DeclarationCommentAlignment = TrailingCommentAlignment.None;
            }));
    }

    [Fact]
    public void Very_long_names_do_not_stretch_the_block()
    {
        var input = Lines("VAR", "a : INT;", "bThisIsAnExtremelyLongVariableNameForTesting : BOOL;", "nB : INT;", "END_VAR");
        Assert.Equal(Lines(
            "VAR",
            "    a  : INT;",
            "    bThisIsAnExtremelyLongVariableNameForTesting : BOOL;",
            "    nB : INT;",
            "END_VAR"), Decl(input, o => o.MaxAlignedNameLength = 20));
    }

    [Fact]
    public void Aligns_struct_members_and_enum_values()
    {
        var structInput = Lines("TYPE ST_Motor :", "STRUCT", "bOn:BOOL;", "fSpeedSetpoint:LREAL:=0.0;", "END_STRUCT", "END_TYPE");
        Assert.Equal(Lines(
            "TYPE ST_Motor :",
            "STRUCT",
            "    bOn            : BOOL;",
            "    fSpeedSetpoint : LREAL := 0.0;",
            "END_STRUCT",
            "END_TYPE"), Decl(structInput));

        var enumInput = Lines("TYPE E_Mode :", "(", "Idle:=0, // nothing", "Running:=10,", "Error", ") INT;", "END_TYPE");
        Assert.Equal(Lines(
            "TYPE E_Mode :",
            "(",
            "    Idle    := 0,  // nothing",
            "    Running := 10,",
            "    Error",
            ") INT;",
            "END_TYPE"), Decl(enumInput));
    }

    [Fact]
    public void Multi_line_initializers_are_left_alone()
    {
        var input = Lines("VAR", "aData : ARRAY[1..3] OF INT := [", "1, 2,", "3];", "n : INT;", "END_VAR");
        Assert.Equal(Lines(
            "VAR",
            "    aData : ARRAY[1..3] OF INT := [",
            "        1, 2,",
            "        3];",
            "    n : INT;",
            "END_VAR"), Decl(input));
    }

    [Fact]
    public void Formats_a_complete_function_block_declaration()
    {
        var input = Lines(
            "{attribute 'reflection'}",
            "FUNCTION_BLOCK PUBLIC FB_Axis EXTENDS FB_Base IMPLEMENTS I_Axis,I_Reset",
            "VAR_INPUT",
            "bEnable:BOOL;",
            "fTargetPosition:LREAL;",
            "END_VAR",
            "VAR_OUTPUT",
            "bBusy:BOOL;",
            "END_VAR",
            "VAR",
            "fbTimer:TON;",
            "nState:E_AxisState:=E_AxisState.Idle;",
            "END_VAR");

        Assert.Equal(Lines(
            "{attribute 'reflection'}",
            "FUNCTION_BLOCK PUBLIC FB_Axis EXTENDS FB_Base IMPLEMENTS I_Axis, I_Reset",
            "VAR_INPUT",
            "    bEnable         : BOOL;",
            "    fTargetPosition : LREAL;",
            "END_VAR",
            "VAR_OUTPUT",
            "    bBusy : BOOL;",
            "END_VAR",
            "VAR",
            "    fbTimer : TON;",
            "    nState  : E_AxisState := E_AxisState.Idle;",
            "END_VAR"), Decl(input));
    }
}
