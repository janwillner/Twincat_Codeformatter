using TcFormatter.Formatting;
using TcFormatter.Naming;

namespace TcFormatter.Core.Tests;

/// <summary>
/// The conventions of plc-coding-guidelines.md (company guideline) must be expressible through configuration.
/// </summary>
public class CompanyGuidelineTests : FormatterTestBase
{
    // ------------------------------------------------------------------ formatting

    [Fact]
    public void F2_style_calls_have_no_space_before_argument_assignment()
    {
        Assert.Equal("fbTimer(IN:= bStart, PT:= T#1S, Q=> bDone);\nx := 1;",
            Impl("fbTimer(IN := bStart, PT := T#1S, Q => bDone); x:=1;", o => o.SpaceBeforeArgumentAssignment = false));
    }

    [Fact]
    public void Wrapped_conditions_can_continue_without_indent()
    {
        var input = Lines("IF bA", "AND bB THEN", "x := 1;", "END_IF");
        Assert.Equal(Lines("IF bA", "AND bB THEN", "    x := 1;", "END_IF"), Impl(input, o => o.IndentConditionContinuation = false));
        Assert.Equal(Lines("IF bA", "    AND bB THEN", "    x := 1;", "END_IF"), Impl(input));
    }

    [Fact]
    public void Wrapped_assignments_can_be_aligned_under_the_assignment_operator()
    {
        var input = Lines("IF a THEN", "bResult := bA AND bB", "AND bC;", "END_IF");
        Assert.Equal(Lines("IF a THEN", "    bResult := bA AND bB", "            AND bC;", "END_IF"),
            Impl(input, o => o.AlignContinuationToAssignment = true));
    }

    [Fact]
    public void Declaration_types_can_start_at_a_fixed_column()
    {
        // name indented one tab, type at tab 10 (tab width 4) = column 37
        var input = Lines("VAR", "b_Enable : BOOL;", "lr_TempDistanceMaster : LREAL := 0;", "END_VAR");
        var lines = Decl(input, o => o.DeclarationTypeColumn = 37).Split('\n');

        Assert.Equal(36, lines[1].IndexOf("BOOL", StringComparison.Ordinal));   // 0-based 36 = column 37
        Assert.Equal(36, lines[2].IndexOf("LREAL", StringComparison.Ordinal));
        Assert.Equal("    b_Enable" + new string(' ', 22) + ": BOOL;", lines[1]);
    }

    // ------------------------------------------------------------------ naming

    private static NamingRules GuidelineRules()
    {
        var r = new NamingRules();
        r.TypePrefixes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["BOOL"] = "b_", ["BYTE"] = "by_", ["WORD"] = "w_", ["DWORD"] = "dw_", ["SINT"] = "sn_", ["INT"] = "n_",
            ["DINT"] = "dn_", ["USINT"] = "usn_", ["UINT"] = "un_", ["UDINT"] = "udn_", ["REAL"] = "r_", ["LREAL"] = "lr_",
            ["STRING"] = "str_", ["TIME"] = "time_", ["DATE"] = "date_", ["TOD"] = "dayt_", ["DT"] = "datet_",
            ["TON"] = "ton_", ["TOF"] = "tof_", ["TP"] = "tp_", ["R_TRIG"] = "osr_", ["F_TRIG"] = "osf_",
            ["CTU"] = "ctu_", ["CTD"] = "ctd_", ["AXIS_REF"] = "ax_", ["_FB_*"] = "fb_", ["T*"] = "",
        };
        r.PointerPrefix = "p_";
        r.ArrayPrefix = "a_";
        r.ConstantPrefix = "";
        r.ConstantNameCase = NameCase.UpperCase;
        r.ScopePrefixes["VAR_INPUT"] = "vi_";
        r.ScopePrefixes["VAR_OUTPUT"] = "vo_";
        r.ScopePrefixes["VAR_IN_OUT"] = "vio_";
        r.MethodLocalMarker = "Temp";
        r.AllowLeadingUnderscore = true;
        r.EnumValueCase = NameCase.UpperCase;
        r.ObjectPrefixes["FUNCTION_BLOCK"] = "_FB_";
        r.ObjectPrefixes["FUNCTION"] = "_FC_";
        r.ObjectPrefixes["METHOD"] = "m_";
        r.ObjectPrefixes["ACTION"] = "act_";
        r.ObjectPrefixes["ENUM"] = "";
        r.ObjectPrefixes["STRUCT"] = "T";
        return r;
    }

    [Fact]
    public void Accepts_code_that_follows_the_guideline()
    {
        var fb = """
            FUNCTION_BLOCK _FB_EmtCrashCheck
            VAR_INPUT
                vi_b_Init : BOOL;
                vi_Data : TCamAnalyse_In;
            END_VAR
            VAR_OUTPUT
                vo_str_ErrorText : STRING;
            END_VAR
            VAR
                b_EnableAxis : BOOL;
                ton_Delay : TON;
                osr_Start : R_TRIG;
                fb_CylinderBrake : _FB_CylinderBrake;
                p_Data : POINTER TO TCamAnalyse_In;
            END_VAR
            VAR CONSTANT
                dn_MAX_LIGHT_CYCLES : DINT := 10;
            END_VAR
            """;
        Assert.Empty(NamingChecker.Check(fb, GuidelineRules()));

        var method = "METHOD m_GetPivotAngle : LREAL\nVAR_INPUT\n    vi_lr_Y : LREAL;\nEND_VAR\nVAR\n    lr_TempAngle : LREAL;\nEND_VAR";
        Assert.Empty(NamingChecker.Check(method, GuidelineRules()));

        var enumType = "{attribute 'qualified_only'}\nTYPE SM_STATE :\n(\n    INIT := 0,\n    OPEN_GEAR := 1\n);\nEND_TYPE";
        Assert.Empty(NamingChecker.Check(enumType, GuidelineRules()));

        Assert.Empty(NamingChecker.CheckObjectName("act_Sequencer", "ACTION", GuidelineRules()));
    }

    [Fact]
    public void Reports_violations_of_the_guideline()
    {
        var fb = """
            FUNCTION_BLOCK _FB_X
            VAR_INPUT
                b_Init : BOOL;
            END_VAR
            VAR CONSTANT
                dn_MaxCycles : DINT := 10;
            END_VAR
            """;
        Assert.Equal(new[] { "b_Init", "dn_MaxCycles" }, NamingChecker.Check(fb, GuidelineRules()).Select(d => d.Name));

        var method = "METHOD m_Calc : LREAL\nVAR\n    lr_Angle : LREAL;\nEND_VAR";
        var d = Assert.Single(NamingChecker.Check(method, GuidelineRules()));
        Assert.Equal(("lr_Angle", "lr_TempAngle"), (d.Name, d.Suggestion));

        var enumType = "TYPE SM_STATE :\n(\n    Init := 0\n);\nEND_TYPE";
        Assert.Equal("INIT", Assert.Single(NamingChecker.Check(enumType, GuidelineRules())).Suggestion);
    }

    [Fact]
    public void The_shipped_preset_loads_and_accepts_guideline_code()
    {
        string dir = AppContext.BaseDirectory;
        while (!File.Exists(Path.Combine(dir, "TcFormatter.sln")))
            dir = Path.GetDirectoryName(dir)!;
        var config = Configuration.TcFormatConfig.Load(Path.Combine(dir, "presets", "company-guideline.tcformat.json"), out var warnings);

        Assert.Empty(warnings);
        Assert.Equal(37, config.Format.DeclarationTypeColumn);
        Assert.False(config.Naming.TypePrefixes.ContainsKey("FB_*"));

        var fb = "FUNCTION_BLOCK _FB_X\nVAR_INPUT\n    vi_b_Init : BOOL;\n    vi_Data : TCamAnalyse_In;\nEND_VAR\nVAR\n    ton_Delay : TON;\n    fb_Brake : _FB_Brake;\nEND_VAR\nVAR CONSTANT\n    dn_MAX_LIGHT_CYCLES : DINT := 10;\nEND_VAR";
        Assert.Empty(NamingChecker.Check(fb, config.Naming));
    }

    [Fact]
    public void Leading_underscore_is_still_reported_by_default()
    {
        var d = Assert.Single(NamingChecker.Check("FUNCTION_BLOCK _FB_X", new NamingRules()));
        Assert.Equal(NamingRuleIds.InvalidIdentifier, d.RuleId);
    }
}
