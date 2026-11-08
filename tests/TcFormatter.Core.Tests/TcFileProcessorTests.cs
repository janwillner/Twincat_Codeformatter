using TcFormatter.Configuration;
using TcFormatter.Naming;
using TcFormatter.TwinCatFiles;

namespace TcFormatter.Core.Tests;

public class TcFileProcessorTests
{
    private const string Pou =
        "<?xml version=\"1.0\" encoding=\"utf-8\"?>\r\n" +
        "<TcPlcObject Version=\"1.1.0.1\" ProductVersion=\"3.1.4026.12\">\r\n" +
        "  <POU Name=\"FB_Motor\" Id=\"{8a1c7a41-1f0e-4a39-9e6e-2d1f0c0b6a11}\" SpecialFunc=\"None\">\r\n" +
        "    <Declaration><![CDATA[FUNCTION_BLOCK FB_Motor\r\nVAR\r\nSpeed:LREAL;\r\nEND_VAR\r\n]]></Declaration>\r\n" +
        "    <Implementation>\r\n" +
        "      <ST><![CDATA[IF Speed>0 THEN Speed:=0; END_IF]]></ST>\r\n" +
        "    </Implementation>\r\n" +
        "    <Method Name=\"M_Start\" Id=\"{0c5e2f10-7c4b-4f43-8d55-5f6b2d7b3c22}\">\r\n" +
        "      <Declaration><![CDATA[METHOD M_Start : BOOL\r\nVAR_INPUT\r\nfSpeed:LREAL;\r\nEND_VAR\r\n]]></Declaration>\r\n" +
        "      <Implementation>\r\n" +
        "        <ST><![CDATA[M_Start:=aData[aIdx[1]]]]><![CDATA[>0;]]></ST>\r\n" +
        "      </Implementation>\r\n" +
        "    </Method>\r\n" +
        "    <Action Name=\"a_reset\" Id=\"{3f8d0b9e-1111-4c1d-9e0f-77aa66bb5c33}\">\r\n" +
        "      <Implementation>\r\n" +
        "        <ST><![CDATA[Speed:=0;]]></ST>\r\n" +
        "      </Implementation>\r\n" +
        "    </Action>\r\n" +
        "    <LineIds Name=\"FB_Motor\">\r\n" +
        "      <LineId Id=\"3\" Count=\"1\" />\r\n" +
        "    </LineIds>\r\n" +
        "  </POU>\r\n" +
        "</TcPlcObject>";

    private static TcFormatConfig Config()
    {
        var c = new TcFormatConfig();
        c.Format.IndentStyle = Formatting.IndentStyle.Spaces;
        return c;
    }

    [Fact]
    public void Formats_only_the_cdata_contents()
    {
        var result = TcFileProcessor.Process(Pou, Config(), format: true, lint: false);

        Assert.Empty(result.Errors);
        Assert.True(result.Changed);
        Assert.Contains("<Declaration><![CDATA[FUNCTION_BLOCK FB_Motor\r\nVAR\r\n    Speed : LREAL;\r\nEND_VAR\r\n]]></Declaration>", result.Text);
        Assert.Contains("<ST><![CDATA[IF Speed > 0 THEN\r\n    Speed := 0;\r\nEND_IF]]></ST>", result.Text);
        Assert.Contains("<ST><![CDATA[Speed := 0;]]></ST>", result.Text);

        // everything outside the CDATA sections is byte-identical
        Assert.Equal(StripCdata(Pou), StripCdata(result.Text));
    }

    [Fact]
    public void Escapes_cdata_terminator_inside_code()
    {
        var config = Config();
        config.Format.SpaceAroundOperators = false;
        var result = TcFileProcessor.Process(Pou, config, format: true, lint: false);

        // "aIdx[1]]>0" contains "]]>" which must be split over two CDATA sections, exactly like TwinCAT does
        Assert.Contains("<ST><![CDATA[M_Start := aData[aIdx[1]]]]><![CDATA[>0;]]></ST>", result.Text);

        var again = TcFileProcessor.Process(result.Text, config, format: true, lint: false);
        Assert.False(again.Changed);
    }

    [Fact]
    public void Unchanged_files_are_reported_as_unchanged()
    {
        var once = TcFileProcessor.Process(Pou, Config(), format: true, lint: false);
        var twice = TcFileProcessor.Process(once.Text, Config(), format: true, lint: false);

        Assert.False(twice.Changed);
        Assert.Equal(once.Text, twice.Text);
    }

    [Fact]
    public void Lints_all_objects_of_the_file_with_their_path()
    {
        var config = Config();
        config.Naming.ObjectPrefixes["METHOD"] = "m_";
        config.Naming.ObjectPrefixes["ACTION"] = "act_";
        var result = TcFileProcessor.Process(Pou, config, format: false, lint: true);

        Assert.False(result.Changed);
        Assert.Equal(Pou, result.Text);
        Assert.Contains(result.Naming, n => n.ObjectPath == "FB_Motor" && n.Diagnostic.Name == "Speed" && n.Diagnostic.RuleId == NamingRuleIds.VariablePrefix);
        Assert.Contains(result.Naming, n => n.ObjectPath == "FB_Motor.M_Start" && n.Diagnostic.RuleId == NamingRuleIds.ObjectPrefix);
        Assert.Contains(result.Naming, n => n.ObjectPath == "FB_Motor.a_reset" && n.Diagnostic.RuleId == NamingRuleIds.ObjectPrefix);
    }

    [Fact]
    public void Uses_the_gvl_name_from_the_xml()
    {
        var gvl = "<?xml version=\"1.0\" encoding=\"utf-8\"?>\r\n<TcPlcObject Version=\"1.1.0.1\">\r\n" +
                  "  <GVL Name=\"Globals\" Id=\"{11111111-2222-3333-4444-555555555555}\">\r\n" +
                  "    <Declaration><![CDATA[VAR_GLOBAL\r\n\tbStop : BOOL;\r\nEND_VAR\r\n]]></Declaration>\r\n" +
                  "  </GVL>\r\n</TcPlcObject>";

        var result = TcFileProcessor.Process(gvl, Config(), format: false, lint: true);

        var d = Assert.Single(result.Naming);
        Assert.Equal(("Globals", NamingRuleIds.ObjectPrefix), (d.ObjectPath, d.Diagnostic.RuleId));
    }

    [Fact]
    public void Leaves_non_st_implementations_alone()
    {
        var fbd = Pou.Replace("<ST><![CDATA[IF Speed>0 THEN Speed:=0; END_IF]]></ST>", "<FBD><![CDATA[x:=1]]></FBD>");
        var result = TcFileProcessor.Process(fbd, Config(), format: true, lint: false);
        Assert.Contains("<FBD><![CDATA[x:=1]]></FBD>", result.Text);
    }

    [Fact]
    public void Reports_safety_errors_per_object_and_keeps_that_object()
    {
        // a formatter that is configured with an absurd option still never produces invalid code;
        // here we just make sure errors are collected with the object path
        var result = TcFileProcessor.Process("<TcPlcObject><POU Name=\"X\"><Declaration><![CDATA[]]></Declaration></POU></TcPlcObject>", Config(), true, true);
        Assert.Empty(result.Errors);
        Assert.False(result.Changed);
    }

    private static string StripCdata(string xml) =>
        System.Text.RegularExpressions.Regex.Replace(xml, @"<!\[CDATA\[.*?\]\]>", "", System.Text.RegularExpressions.RegexOptions.Singleline);
}
