using TcFormatter.Naming;

namespace TcFormatter.Core.Tests;

public class NamingCheckerTests
{
    private static List<NamingDiagnostic> Check(string declaration, Action<NamingRules>? configure = null, string? objectName = null)
    {
        var rules = new NamingRules();
        configure?.Invoke(rules);
        return NamingChecker.Check(declaration, rules, objectName).ToList();
    }

    [Fact]
    public void Accepts_a_correctly_named_function_block()
    {
        var decl = """
            FUNCTION_BLOCK FB_Motor IMPLEMENTS I_Motor
            VAR_INPUT
                bEnable : BOOL;
                nCount, iIndex : INT;
                fSpeed : LREAL;
                sName : STRING(80);
                wsText : WSTRING;
                tDelay : TIME;
                dtStamp : DT;
                todAlarm : TOD;
            END_VAR
            VAR
                aValues : ARRAY[1..10] OF INT;
                pData : POINTER TO ST_Data;
                refAxis : REFERENCE TO FB_Axis;
                stData : ST_Data;
                eState : E_State := E_State.Idle;
                fbTimer : TON;
                fbEdge : R_TRIG;
                fbAxis : FB_Axis(nId := 5);
                iMotor : I_Motor;
                bIn AT %I* : BOOL;
            END_VAR
            VAR CONSTANT
                cMax : INT := 5;
            END_VAR
            """;

        Assert.Empty(Check(decl));
    }

    [Fact]
    public void Reports_variables_with_wrong_type_prefix_and_suggests_a_name()
    {
        var decl = "PROGRAM MAIN\nVAR\n    Speed : LREAL;\n    counter : INT;\n    bValue : INT;\n    bOk : BOOL;\nEND_VAR";

        var result = Check(decl);

        Assert.Collection(result,
            d =>
            {
                Assert.Equal(NamingRuleIds.VariablePrefix, d.RuleId);
                Assert.Equal("Speed", d.Name);
                Assert.Equal("fSpeed", d.Suggestion);
                Assert.Equal(3, d.Line);
                Assert.Equal(5, d.Column);
            },
            d => Assert.Equal(("counter", "nCounter"), (d.Name, d.Suggestion)),
            d => Assert.Equal(("bValue", "nValue"), (d.Name, d.Suggestion)));
    }

    [Fact]
    public void Suggestion_replaces_a_wrong_lower_case_prefix()
    {
        var result = Check("FUNCTION_BLOCK FB_X\nVAR\n    xpMotor : I_Motor;\n    bstart : BOOL;\n    ipMotor : I_Motor;\nEND_VAR");
        Assert.Equal(new[] { ("xpMotor", "iMotor"), ("bstart", "bStart") }, result.Select(d => (d.Name, d.Suggestion)));
    }

    [Fact]
    public void Requires_an_upper_case_letter_after_the_prefix()
    {
        var result = Check("FUNCTION_BLOCK FB_X\nVAR\n    bstart : BOOL;\n    number : INT;\nEND_VAR");
        Assert.Equal(new[] { "bstart", "number" }, result.Select(d => d.Name));
    }

    [Theory]
    [InlineData("FUNCTION_BLOCK Motor", "FB_")]
    [InlineData("FUNCTION Scale : INT", "F_")]
    [InlineData("PROGRAM Prg_Main", "P_")]
    [InlineData("INTERFACE Motor", "I_")]
    [InlineData("TYPE Data :\nSTRUCT\nEND_STRUCT\nEND_TYPE", "ST_")]
    [InlineData("TYPE State :\n(\n    Idle\n);\nEND_TYPE", "E_")]
    [InlineData("TYPE Name : STRING(40);\nEND_TYPE", "T_")]
    public void Reports_objects_without_prefix(string declaration, string prefix)
    {
        var d = Assert.Single(Check(declaration));
        Assert.Equal(NamingRuleIds.ObjectPrefix, d.RuleId);
        Assert.Contains(prefix, d.Message);
    }

    [Fact]
    public void Main_program_is_an_allowed_exception()
    {
        Assert.Empty(Check("PROGRAM MAIN\nVAR\nEND_VAR"));
    }

    [Fact]
    public void Gvl_name_comes_from_the_object_name()
    {
        Assert.Empty(Check("VAR_GLOBAL\n    bStop : BOOL;\nEND_VAR", objectName: "GVL_Main"));
        var d = Assert.Single(Check("VAR_GLOBAL\n    bStop : BOOL;\nEND_VAR", objectName: "Globals"));
        Assert.Equal(NamingRuleIds.ObjectPrefix, d.RuleId);
    }

    [Fact]
    public void Methods_and_properties_use_plain_pascal_case()
    {
        Assert.Empty(Check("METHOD Start : BOOL\nVAR_INPUT\n    fSpeed : LREAL;\nEND_VAR"));
        Assert.Empty(Check("METHOD FB_init : BOOL\nVAR_INPUT\n    bInitRetains : BOOL;\n    bInCopyCode : BOOL;\nEND_VAR"));
        Assert.Equal(NamingRuleIds.MemberName, Assert.Single(Check("METHOD M_Start : BOOL")).RuleId);
        Assert.Equal(NamingRuleIds.MemberName, Assert.Single(Check("PROPERTY PUBLIC speed : LREAL")).RuleId);
    }

    [Fact]
    public void Checks_struct_members_and_enum_values()
    {
        var structDecl = "TYPE ST_Data :\nSTRUCT\n    bOk : BOOL;\n    value : INT;\nEND_STRUCT\nEND_TYPE";
        Assert.Equal("value", Assert.Single(Check(structDecl)).Name);

        var enumDecl = "TYPE E_State :\n(\n    Idle := 0,\n    running\n);\nEND_TYPE";
        var d = Assert.Single(Check(enumDecl));
        Assert.Equal(("running", NamingRuleIds.EnumValue), (d.Name, d.RuleId));
    }

    [Fact]
    public void Prefixes_are_configurable()
    {
        var decl = "FUNCTION_BLOCK FB_X\nVAR\n    xStart : BOOL;\n    arrData : ARRAY[0..1] OF INT;\n    ipMotor : I_Motor;\nEND_VAR";
        Assert.NotEmpty(Check(decl));
        Assert.Empty(Check(decl, r =>
        {
            r.TypePrefixes["BOOL"] = "x";
            r.ArrayPrefix = "arr";
            r.TypePrefixes["I_*"] = "ip";
        }));
    }

    [Fact]
    public void Array_and_pointer_prefixes_can_include_the_element_prefix()
    {
        var decl = "FUNCTION_BLOCK FB_X\nVAR\n    abFlags : ARRAY[0..1] OF BOOL;\n    pnValue : POINTER TO INT;\nEND_VAR";
        Assert.Empty(Check(decl, r => { r.ArrayIncludesElementPrefix = true; r.PointerIncludesTargetPrefix = true; }));
        Assert.Equal(2, Check(decl).Count);
    }

    [Fact]
    public void Scope_prefixes_are_required_when_configured()
    {
        var decl = "VAR_GLOBAL\n    gbStop : BOOL;\n    bRun : BOOL;\nEND_VAR";
        var d = Assert.Single(Check(decl, r => r.ScopePrefixes["VAR_GLOBAL"] = "g", objectName: "GVL_X"));
        Assert.Equal(("bRun", "gbRun"), (d.Name, d.Suggestion));
    }

    [Fact]
    public void Ignore_comment_and_ignore_list_suppress_diagnostics()
    {
        var decl = "FUNCTION_BLOCK FB_X\nVAR\n    i : INT; // tcfmt: ignore-naming\n    j : INT;\n    k : INT;\nEND_VAR";
        Assert.Equal(new[] { "j", "k" }, Check(decl).Select(d => d.Name));
        Assert.Empty(Check(decl, r => r.IgnoreNames.Add("^[jk]$")));
    }

    [Fact]
    public void Rules_can_be_switched_off_or_change_severity()
    {
        var decl = "FUNCTION_BLOCK Motor\nVAR\n    Speed : LREAL;\nEND_VAR";
        var result = Check(decl, r =>
        {
            r.Severities[NamingRuleIds.ObjectPrefix] = NamingSeverity.Off;
            r.Severities[NamingRuleIds.VariablePrefix] = NamingSeverity.Error;
        });
        var d = Assert.Single(result);
        Assert.Equal(NamingSeverity.Error, d.Severity);
        Assert.Empty(Check(decl, r => r.Enabled = false));
    }

    [Fact]
    public void Unknown_types_are_not_checked()
    {
        Assert.Empty(Check("FUNCTION_BLOCK FB_X\nVAR\n    Axis : AXIS_REF;\nEND_VAR"));
    }
}
