using TcFormatter.Cli;

namespace TcFormatter.Core.Tests;

public sealed class CliTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "tcfmt-cli-" + Guid.NewGuid().ToString("N"));

    public CliTests()
    {
        Directory.CreateDirectory(Path.Combine(_dir, "POUs"));
        File.WriteAllText(Path.Combine(_dir, "POUs", "FB_A.TcPOU"),
            "﻿<?xml version=\"1.0\" encoding=\"utf-8\"?>\r\n<TcPlcObject>\r\n  <POU Name=\"FB_A\" Id=\"{00000000-0000-0000-0000-000000000001}\">\r\n" +
            "    <Declaration><![CDATA[FUNCTION_BLOCK FB_A\r\nVAR\r\nSpeed:LREAL;\r\nEND_VAR\r\n]]></Declaration>\r\n" +
            "    <Implementation>\r\n      <ST><![CDATA[Speed:=1;]]></ST>\r\n    </Implementation>\r\n  </POU>\r\n</TcPlcObject>");
    }

    public void Dispose() => Directory.Delete(_dir, true);

    private (int Exit, string Out, string Err) Run(params string[] args)
    {
        var stdout = new StringWriter();
        var stderr = new StringWriter();
        int exit = Program.Run(args.Append("--no-user-config").ToArray(), new StringReader(""), stdout, stderr);
        return (exit, stdout.ToString(), stderr.ToString());
    }

    [Fact]
    public void Check_format_check_cycle()
    {
        var file = Path.Combine(_dir, "POUs", "FB_A.TcPOU");

        Assert.Equal(Program.CheckFailed, Run("check", _dir).Exit);
        var before = File.ReadAllBytes(file);

        var format = Run("format", _dir);
        Assert.Equal(Program.Ok, format.Exit);
        Assert.Contains("1 file(s) reformatted", format.Out);

        var after = File.ReadAllBytes(file);
        Assert.Equal(new byte[] { 0xEF, 0xBB, 0xBF }, after.Take(3)); // BOM kept
        Assert.NotEqual(before, after);

        Assert.Equal(Program.Ok, Run("check", _dir).Exit);
    }

    [Fact]
    public void Lint_fails_only_at_the_requested_severity()
    {
        Assert.Equal(Program.Ok, Run("lint", _dir).Exit);
        var warning = Run("lint", "--fail-on", "warning", _dir);
        Assert.Equal(Program.CheckFailed, warning.Exit);
        Assert.Contains("FB_A.TcPOU(6,1): warning TCN002", warning.Out);
    }

    [Fact]
    public void Init_writes_a_configuration_that_is_picked_up()
    {
        Assert.Equal(Program.Ok, Run("init", _dir).Exit);
        var config = Path.Combine(_dir, ".tcformat.json");
        Assert.True(File.Exists(config));
        File.WriteAllText(config, File.ReadAllText(config).Replace("\"indentStyle\": \"Tabs\"", "\"indentStyle\": \"Spaces\""));

        Run("format", _dir);
        Assert.Contains("    Speed : LREAL;", File.ReadAllText(Path.Combine(_dir, "POUs", "FB_A.TcPOU")));
        Assert.Equal(Program.UsageError, Run("init", _dir).Exit); // exists
    }

    [Fact]
    public void Import_stweep_writes_a_configuration()
    {
        var stweep = Path.Combine(_dir, "my.stweep");
        File.WriteAllText(stweep, """{ "Settings": { "TabsIndentAlignment": { "IndentStyle": 0, "IndentSize": 2 }, "Spaces": { "AroundDot": true } }, "Version": "4.5.0.0" }""");

        var result = Run("import-stweep", stweep, _dir);

        Assert.Equal(Program.Ok, result.Exit);
        Assert.Contains("AroundDot", result.Out);
        var config = Configuration.TcFormatConfig.Load(Path.Combine(_dir, ".tcformat.json"), out var warnings);
        Assert.Empty(warnings);
        Assert.Equal(2, config.Format.IndentSize);
        Assert.Equal(Formatting.IndentStyle.Spaces, config.Format.IndentStyle);

        Assert.Equal(Program.UsageError, Run("import-stweep", stweep, _dir).Exit); // exists
        Assert.Equal(Program.Ok, Run("import-stweep", stweep, _dir, "--force").Exit);
    }

    [Fact]
    public void Stdin_formats_text()
    {
        var stdout = new StringWriter();
        int exit = Program.Run(new[] { "stdin", "--part", "impl", "--no-user-config" }, new StringReader("a:=1;"), stdout, new StringWriter());
        Assert.Equal((Program.Ok, "a := 1;"), (exit, stdout.ToString()));
    }

    [Fact]
    public void Bad_arguments_give_usage_error()
    {
        Assert.Equal(Program.UsageError, Run("format", "--nope").Exit);
        Assert.Equal(Program.UsageError, Run("format", Path.Combine(_dir, "missing")).Exit);
    }
}
