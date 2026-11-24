using TcFormatter.Configuration;
using TcFormatter.Formatting;
using TcFormatter.Objects;

namespace TcFormatter.Core.Tests;

/// <summary>
/// Processing one Objekt: the flow that the CLI (XML files) and the IDE (TwinCAT project tree) share, tested through an
/// in-memory adapter.
/// </summary>
public class ObjectProcessorTests
{
    private sealed class FakeObject(string name, PlcObjectKind kind, string? declaration, string? implementation) : IPlcObject
    {
        public Dictionary<CodePart, string> Parts { get; } = new();
        public List<CodePart> Writes { get; } = new();
        public bool ThrowOnRead { get; init; }

        public string Path => "PLC^POUs^" + name;
        public string Name => name;
        public PlcObjectKind Kind => kind;

        private bool _initialised;

        private void Init()
        {
            if (_initialised)
                return;
            _initialised = true;
            if (declaration != null) Parts[CodePart.Declaration] = declaration;
            if (implementation != null) Parts[CodePart.Implementation] = implementation;
        }

        public bool Has(CodePart part)
        {
            Init();
            return Parts.ContainsKey(part);
        }

        public string Read(CodePart part)
        {
            Init();
            if (ThrowOnRead)
                throw new InvalidOperationException("COM busy");
            return Parts[part];
        }

        public void Write(CodePart part, string text)
        {
            Parts[part] = text;
            Writes.Add(part);
        }
    }

    private static TcFormatConfig Config() => new();

    [Fact]
    public void Formats_both_code_parts_and_writes_only_what_changed()
    {
        var obj = new FakeObject("FB_A", PlcObjectKind.Other, "FUNCTION_BLOCK FB_A\nVAR\n\tnA : INT;\nEND_VAR\n", "nA:=1;");

        var result = ObjectProcessor.Process(obj, Config(), format: true, lint: false);

        Assert.True(result.Changed);
        Assert.Empty(result.Errors);
        Assert.Equal(new[] { CodePart.Implementation }, obj.Writes);
        Assert.Equal("nA := 1;", obj.Parts[CodePart.Implementation]);
    }

    [Fact]
    public void Naming_is_checked_on_the_formatted_declaration()
    {
        var obj = new FakeObject("FB_A", PlcObjectKind.Other, "FUNCTION_BLOCK FB_A\nVAR a:INT; b:INT; Speed:LREAL; END_VAR", null);

        var result = ObjectProcessor.Process(obj, Config(), format: true, lint: true);

        var speed = Assert.Single(result.Naming, n => n.Diagnostic.Name == "Speed");
        Assert.Equal(CodePart.Declaration, speed.Part);
        var line = obj.Parts[CodePart.Declaration].Split('\n')[speed.Diagnostic.Line - 1];
        Assert.Equal("Speed", line.Substring(speed.Diagnostic.Column - 1, 5));
    }

    [Fact]
    public void The_name_of_an_action_is_checked_once()
    {
        var obj = new FakeObject("ActReset", PlcObjectKind.Action, null, "x := 1;");
        var config = Config();
        config.Naming.ObjectPrefixes["ACTION"] = "act_";

        var result = ObjectProcessor.Process(obj, config, format: true, lint: true);

        var finding = Assert.Single(result.Naming);
        Assert.Null(finding.Part);
        Assert.Equal("ActReset", finding.Diagnostic.Name);
    }

    [Fact]
    public void The_name_of_a_gvl_comes_from_the_object()
    {
        var obj = new FakeObject("Globals", PlcObjectKind.Gvl, "VAR_GLOBAL\n\tnA : INT;\nEND_VAR", null);

        var result = ObjectProcessor.Process(obj, Config(), format: false, lint: true);

        Assert.Contains(result.Naming, n => n.Diagnostic.Name == "Globals");
    }

    [Fact]
    public void A_refused_code_part_is_reported_and_not_written()
    {
        var obj = new FakeObject("FB_A", PlcObjectKind.Other, null, "IF a THEN x := 1;");

        var result = ObjectProcessor.Process(obj, Config(), format: true, lint: false);

        var error = Assert.Single(result.Errors);
        Assert.StartsWith("PLC^POUs^FB_A (Implementation): Syntax error, code left unchanged: line 1", error);
        Assert.Empty(obj.Writes);
        Assert.False(result.Changed);
    }

    [Fact]
    public void A_syntax_error_in_the_declaration_is_reported_once()
    {
        var obj = new FakeObject("FB_A", PlcObjectKind.Other, "FUNCTION_BLOCK FB_A\nVAR\n\tnA : INT\nEND_VAR", null);

        var formatAndLint = ObjectProcessor.Process(obj, Config(), format: true, lint: true);
        var lintOnly = ObjectProcessor.Process(obj, Config(), format: false, lint: true);

        Assert.EndsWith("(names not checked)", Assert.Single(formatAndLint.Errors));
        Assert.Empty(formatAndLint.Naming);
        Assert.Empty(lintOnly.Errors);
        Assert.Equal(Naming.NamingRuleIds.SyntaxError, Assert.Single(lintOnly.Naming).Diagnostic.RuleId);
    }

    [Fact]
    public void A_failing_object_is_reported_instead_of_stopping_the_run()
    {
        var obj = new FakeObject("FB_A", PlcObjectKind.Other, "FUNCTION_BLOCK FB_A", null) { ThrowOnRead = true };

        var result = ObjectProcessor.Process(obj, Config(), format: true, lint: true);

        Assert.Equal("PLC^POUs^FB_A: InvalidOperationException: COM busy", Assert.Single(result.Errors));
    }
}
