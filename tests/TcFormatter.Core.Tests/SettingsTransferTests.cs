using TcFormatter.Configuration;
using TcFormatter.Formatting;

namespace TcFormatter.Core.Tests;

public class SettingsTransferTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "tcfmt-transfer-" + Guid.NewGuid().ToString("N"));

    public SettingsTransferTests() => Directory.CreateDirectory(_dir);

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private static string Preset(string name)
    {
        string dir = AppContext.BaseDirectory;
        while (!File.Exists(Path.Combine(dir, "TcFormatter.sln")))
            dir = Path.GetDirectoryName(dir)!;
        return Path.Combine(dir, "presets", name);
    }

    [Fact]
    public void Importing_a_tcformat_file_replaces_format_and_naming()
    {
        var current = new TcFormatConfig();
        current.Naming.ArrayPrefix = "arr";

        var imported = SettingsTransfer.Import(Preset("Syntegon.tcformat.json"), current, out var notes);

        Assert.Empty(notes);
        Assert.Equal(43, imported.Format.DeclarationTypeColumn);
        Assert.Equal("", imported.Naming.ArrayPrefix);
    }

    [Fact]
    public void Importing_STweep_settings_keeps_the_naming_rules()
    {
        var current = new TcFormatConfig();
        current.Naming.ArrayPrefix = "arr";

        var imported = SettingsTransfer.Import(Preset("STSweepConfig.STweep"), current, out _);

        Assert.Equal("arr", imported.Naming.ArrayPrefix);
        Assert.Equal(IndentStyle.Spaces, imported.Format.IndentStyle);
    }

    [Fact]
    public void Export_writes_a_file_that_imports_to_the_same_settings()
    {
        var config = new TcFormatConfig();
        config.Format.IndentSize = 3;
        config.Naming.ScopePrefixes["VAR_INPUT"] = "vi_";
        string file = Path.Combine(_dir, "sub", ".tcformat.json");

        SettingsTransfer.Export(config, file);
        var back = SettingsTransfer.Import(file, new TcFormatConfig(), out var notes);

        Assert.Empty(notes);
        Assert.Equal(3, back.Format.IndentSize);
        Assert.Equal("vi_", back.Naming.ScopePrefixes["VAR_INPUT"]);
        Assert.Equal(config.ToJson(), back.ToJson());
    }

    [Fact]
    public void Invalid_files_are_reported()
    {
        string file = Path.Combine(_dir, "broken.json");
        File.WriteAllText(file, "{ \"format\": { \"indentSize\": ");
        Assert.Throws<ConfigException>(() => SettingsTransfer.Import(file, new TcFormatConfig(), out _));
    }
}
