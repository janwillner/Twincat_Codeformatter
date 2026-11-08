using TcFormatter.Configuration;
using TcFormatter.Formatting;
using TcFormatter.Naming;

namespace TcFormatter.Core.Tests;

public sealed class ConfigurationTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "tcfmt-tests-" + Guid.NewGuid().ToString("N"));

    public ConfigurationTests() => Directory.CreateDirectory(_root);

    public void Dispose() => Directory.Delete(_root, true);

    [Fact]
    public void Missing_keys_keep_their_defaults()
    {
        var config = TcFormatConfig.Parse("""{ "format": { "indentStyle": "spaces", "indentSize": 2 } }""", out var warnings);

        Assert.Empty(warnings);
        Assert.Equal(IndentStyle.Spaces, config.Format.IndentStyle);
        Assert.Equal(2, config.Format.IndentSize);
        Assert.Equal(new FormatterOptions().KeywordCasing, config.Format.KeywordCasing);
        Assert.Equal("b", config.Naming.TypePrefixes["BOOL"]);
    }

    [Fact]
    public void Reads_comments_enums_dictionaries_and_lists()
    {
        var json = """
            // project formatting rules
            {
              "format": {
                "keywordCasing": "Lower", /* lower case keywords */
                "endKeywordSemicolon": "add",
                "maxLineLength": 100,
                "alignDeclarationColons": false
              },
              "naming": {
                "typePrefixes": { "BOOL": "x", "FB_*": "fb" },
                "scopePrefixes": { "VAR_GLOBAL": "g" },
                "ignoreNames": [ "^MAIN$", "^i$" ],
                "severities": { "TCN002": "error" }
              }
            }
            """;

        var config = TcFormatConfig.Parse(json, out var warnings);

        Assert.Empty(warnings);
        Assert.Equal(CasingStyle.Lower, config.Format.KeywordCasing);
        Assert.Equal(SemicolonStyle.Add, config.Format.EndKeywordSemicolon);
        Assert.Equal(100, config.Format.MaxLineLength);
        Assert.False(config.Format.AlignDeclarationColons);
        Assert.Equal("x", config.Naming.TypePrefixes["bool"]);
        Assert.Equal("n,i", config.Naming.TypePrefixes["INT"]); // defaults are merged, not replaced
        Assert.Equal("g", config.Naming.ScopePrefixes["var_global"]);
        Assert.Equal(new[] { "^MAIN$", "^i$" }, config.Naming.IgnoreNames);
        Assert.Equal(NamingSeverity.Error, config.Naming.SeverityOf(NamingRuleIds.VariablePrefix));
    }

    [Fact]
    public void Reports_unknown_keys_and_invalid_values()
    {
        TcFormatConfig.Parse("""{ "format": { "indentSyze": 2, "keywordCasing": "Shouting" }, "colour": 1 }""", out var warnings);

        Assert.Equal(3, warnings.Count);
        Assert.Contains(warnings, w => w.Contains("indentSyze"));
        Assert.Contains(warnings, w => w.Contains("Shouting"));
        Assert.Contains(warnings, w => w.Contains("colour"));
    }

    [Fact]
    public void Invalid_json_is_reported_with_position()
    {
        var ex = Assert.Throws<ConfigException>(() => TcFormatConfig.Parse("{ \"format\": { \"indentSize\": } }", out _));
        Assert.Contains("line 1", ex.Message);
    }

    [Fact]
    public void Saved_default_configuration_round_trips()
    {
        var original = new TcFormatConfig();
        original.Format.IndentSize = 3;
        original.Naming.ScopePrefixes["VAR_GLOBAL"] = "g";

        string text = original.ToJson(includeDescriptions: true);
        var parsed = TcFormatConfig.Parse(text, out var warnings);

        Assert.Empty(warnings);
        Assert.Contains("// Number of spaces per indentation level", text);
        Assert.Equal(text, parsed.ToJson(includeDescriptions: true));
    }

    [Fact]
    public void Finds_the_nearest_configuration_file_upwards()
    {
        var project = Directory.CreateDirectory(Path.Combine(_root, "Plc", "POUs", "Motion")).FullName;
        File.WriteAllText(Path.Combine(_root, ".tcformat.json"), """{ "format": { "indentSize": 8 } }""");
        File.WriteAllText(Path.Combine(_root, "Plc", ".tcformat.json"), """{ "format": { "indentSize": 2 } }""");

        var found = TcFormatConfig.FindFile(project);
        Assert.Equal(Path.Combine(_root, "Plc", ".tcformat.json"), found);

        var config = TcFormatConfig.LoadFor(project, out var source, out _);
        Assert.Equal(2, config.Format.IndentSize);
        Assert.Equal(found, source);
    }

    [Fact]
    public void Uses_defaults_when_no_file_exists()
    {
        var empty = Directory.CreateDirectory(Path.Combine(_root, "nothing")).FullName;
        var config = TcFormatConfig.LoadFor(empty, out var source, out _, stopAt: _root);
        Assert.Null(source);
        Assert.Equal(new FormatterOptions().IndentSize, config.Format.IndentSize);
    }
}
