using TcFormatter.Configuration;
using TcFormatter.TwinCatFiles;

namespace TcFormatter.Core.Tests;

/// <summary>
/// Regression tests on real TwinCAT files: Fixtures/input holds the unformatted objects of the test project
/// as XAE saved them, Fixtures/expected the result that was verified to build with 0 errors in TwinCAT.
/// </summary>
public class GoldenFileTests
{
    private static string Fixtures => Path.Combine(AppContext.BaseDirectory, "Fixtures");

    public static IEnumerable<object[]> Files() =>
        Directory.GetFiles(Path.Combine(Fixtures, "input")).Select(f => new object[] { Path.GetFileName(f) });

    [Theory]
    [MemberData(nameof(Files))]
    public void Formats_real_twincat_files_as_expected(string file)
    {
        string input = File.ReadAllText(Path.Combine(Fixtures, "input", file));
        string expected = File.ReadAllText(Path.Combine(Fixtures, "expected", file));

        var result = TcFileProcessor.Process(input, new TcFormatConfig(), format: true, lint: false);

        Assert.Empty(result.Errors);
        Assert.Equal(expected, result.Text);
    }

    [Theory]
    [MemberData(nameof(Files))]
    public void Expected_files_are_stable(string file)
    {
        string expected = File.ReadAllText(Path.Combine(Fixtures, "expected", file));

        var result = TcFileProcessor.Process(expected, new TcFormatConfig(), format: true, lint: false);

        Assert.False(result.Changed);
    }
}
