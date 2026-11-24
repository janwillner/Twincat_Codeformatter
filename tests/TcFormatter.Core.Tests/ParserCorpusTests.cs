using TcFormatter.Formatting;
using TcFormatter.Naming;
using TcFormatter.Syntax;
using TcFormatter.TwinCatFiles;

namespace TcFormatter.Core.Tests;

/// <summary>
/// The grammar must accept real TwinCAT code: the fixtures, the naming examples and, when it is present on this
/// machine, the large project under TestProject/PLC (not checked in; the test passes without it).
/// </summary>
public class ParserCorpusTests
{
    private static string Fixtures => Path.Combine(AppContext.BaseDirectory, "Fixtures");

    [Fact]
    public void Fixtures_parse_without_syntax_errors()
    {
        var files = Directory.GetFiles(Fixtures, "*", SearchOption.AllDirectories);

        AssertParses(files);
    }

    [Fact]
    public void Naming_examples_parse_without_syntax_errors()
    {
        var failures = NamingOptionExamples.Codes
            .Select(code => (code, errors: StSyntax.Parse(code, CodePart.Declaration)))
            .Where(r => r.errors.Count > 0)
            .Select(r => $"{r.errors[0]}\n{r.code}")
            .ToList();

        Assert.True(failures.Count == 0, string.Join("\n\n", failures));
    }

    [Fact]
    public void Local_test_project_parses_without_syntax_errors()
    {
        var root = FindRepositoryRoot();
        var project = root == null ? null : Path.Combine(root, "TestProject", "PLC");
        if (project == null || !Directory.Exists(project))
            return; // the project is not checked in

        var files = TcFileProcessor.Extensions
            .SelectMany(ext => Directory.GetFiles(project, "*" + ext, SearchOption.AllDirectories))
            .ToList();

        AssertParses(files);
    }

    private static void AssertParses(IEnumerable<string> files)
    {
        var failures = new List<string>();
        var details = new List<string>();
        int sections = 0;
        foreach (var file in files)
        {
            foreach (var (objectPath, part, code) in TcFileProcessor.CodeSections(File.ReadAllText(file)))
            {
                sections++;
                var errors = StSyntax.Parse(code, part);
                if (errors.Count == 0)
                    continue;
                string header = $"{Path.GetFileName(file)} {objectPath} ({part}) {errors[0]}";
                failures.Add($"{header}\n    {LineOf(code, errors[0].Line)}");
                details.Add(header + "\n" + Context(code, errors[0].Line));
            }
        }

        // TCFMT_CORPUS_REPORT=<file>: write every failure with some context (helps when extending the grammar)
        var report = Environment.GetEnvironmentVariable("TCFMT_CORPUS_REPORT");
        if (!string.IsNullOrEmpty(report) && failures.Count > 0)
            File.AppendAllLines(report, details);

        Assert.True(failures.Count == 0,
            $"{failures.Count} of {sections} sections have syntax errors:\n" + string.Join("\n", failures.Take(60)));
    }

    private static string Context(string code, int line)
    {
        var lines = code.Split('\n');
        var sb = new System.Text.StringBuilder();
        for (int i = Math.Max(1, line - 4); i <= Math.Min(lines.Length, line + 1); i++)
            sb.Append(i == line ? "  >> " : "  |  ").Append(lines[i - 1].TrimEnd()).Append('\n');
        return sb.ToString();
    }

    private static string LineOf(string code, int line)
    {
        var lines = code.Split('\n');
        return line >= 1 && line <= lines.Length ? lines[line - 1].Trim() : string.Empty;
    }

    private static string? FindRepositoryRoot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir != null; dir = dir.Parent)
        {
            if (File.Exists(Path.Combine(dir.FullName, "TcFormatter.sln")))
                return dir.FullName;
        }
        return null;
    }
}
