using System.Text;
using TcFormatter.Configuration;
using TcFormatter.Naming;
using TcFormatter.TwinCatFiles;

namespace TcFormatter.Cli;

/// <summary>Runs 'format', 'check' and 'lint' over files, folders and projects.</summary>
internal sealed class Runner(CliOptions options, TextWriter stdout, TextWriter stderr)
{
    private static readonly HashSet<string> SkippedFolders = new(StringComparer.OrdinalIgnoreCase)
    {
        "_Boot", "_CompileInfo", "_Libraries", ".git", ".vs", "bin", "obj",
    };

    private readonly Dictionary<string, TcFormatConfig> _configs = new(StringComparer.OrdinalIgnoreCase);

    public int Format()
    {
        bool write = !options.Check && (!options.Diff || options.Write);
        int changed = 0, unchanged = 0, failed = 0;
        var naming = new List<(string File, ObjectDiagnostic D)>();

        foreach (var file in ExpandPaths())
        {
            var (text, encoding) = Read(file);
            var config = ConfigFor(file);
            var result = TcFileProcessor.Process(text, config, format: true, lint: options.AlsoLint);

            foreach (var error in result.Errors)
            {
                stderr.WriteLine($"{file}: error: {error}");
                failed++;
            }
            naming.AddRange(result.Naming.Select(n => (file, n)));

            if (!result.Changed)
            {
                unchanged++;
                continue;
            }
            changed++;

            if (options.Diff)
                stdout.Write(LineDiff.Unified(file, text, result.Text));

            if (write)
            {
                File.WriteAllText(file, result.Text, encoding);
                if (!options.Quiet)
                    stdout.WriteLine($"reformatted {file}");
            }
            else if (!options.Quiet && !options.Diff)
            {
                stdout.WriteLine($"would reformat {file}");
            }
        }

        int namingFailures = PrintNaming(naming);

        if (!options.Quiet)
        {
            string verb = write ? "reformatted" : "would be reformatted";
            stdout.WriteLine($"{changed} file(s) {verb}, {unchanged} file(s) left unchanged" +
                             (failed > 0 ? $", {failed} object(s) refused by the safety check" : "") + ".");
        }

        if (failed > 0)
            return Program.FormatterError;
        if (!write && changed > 0)
            return Program.CheckFailed;
        return namingFailures > 0 ? Program.CheckFailed : Program.Ok;
    }

    public int Lint()
    {
        var naming = new List<(string File, ObjectDiagnostic D)>();
        int files = 0;
        foreach (var file in ExpandPaths())
        {
            files++;
            var (text, _) = Read(file);
            var result = TcFileProcessor.Process(text, ConfigFor(file), format: false, lint: true);
            naming.AddRange(result.Naming.Select(n => (file, n)));
        }

        int failures = PrintNaming(naming);
        if (!options.Quiet && !options.Json)
            stdout.WriteLine($"{naming.Count} naming issue(s) in {files} file(s).");
        return failures > 0 ? Program.CheckFailed : Program.Ok;
    }

    /// <summary>Prints diagnostics in MSBuild format and returns how many reach the --fail-on level.</summary>
    private int PrintNaming(List<(string File, ObjectDiagnostic D)> naming)
    {
        int failures = 0;
        foreach (var (file, n) in naming)
        {
            var d = n.Diagnostic;
            if (d.Severity == NamingSeverity.Off)
                continue;
            if (!options.FailOnNone && d.Severity >= options.FailOn)
                failures++;

            if (options.Json)
            {
                stdout.WriteLine("{" +
                    $"\"file\":{Q(file)},\"line\":{n.FileLine},\"column\":{n.FileColumn},\"object\":{Q(n.ObjectPath)}," +
                    $"\"rule\":{Q(d.RuleId)},\"severity\":{Q(d.Severity.ToString().ToLowerInvariant())},\"name\":{Q(d.Name)}," +
                    $"\"suggestion\":{Q(d.Suggestion)},\"message\":{Q(d.Message)}" + "}");
            }
            else
            {
                string severity = d.Severity switch
                {
                    NamingSeverity.Error => "error",
                    NamingSeverity.Warning => "warning",
                    _ => "info",
                };
                stdout.WriteLine($"{file}({n.FileLine},{n.FileColumn}): {severity} {d.RuleId}: {d.Message} [{n.ObjectPath}]");
            }
        }
        return failures;
    }

    private static string Q(string? s)
    {
        if (s == null)
            return "null";
        var sb = new StringBuilder("\"");
        foreach (char c in s)
        {
            sb.Append(c switch
            {
                '"' => "\\\"",
                '\\' => "\\\\",
                '\n' => "\\n",
                '\r' => "\\r",
                '\t' => "\\t",
                _ => c.ToString(),
            });
        }
        return sb.Append('"').ToString();
    }

    private TcFormatConfig ConfigFor(string file)
    {
        string directory = Path.GetDirectoryName(Path.GetFullPath(file)) ?? Directory.GetCurrentDirectory();
        if (!_configs.TryGetValue(directory, out var config))
        {
            config = Program.LoadConfig(options, directory, stderr);
            _configs[directory] = config;
        }
        return config;
    }

    private static (string Text, Encoding Encoding) Read(string file)
    {
        byte[] bytes = File.ReadAllBytes(file);
        bool bom = bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF;
        var encoding = new UTF8Encoding(bom);
        string text = Encoding.UTF8.GetString(bytes, bom ? 3 : 0, bytes.Length - (bom ? 3 : 0));
        return (text, encoding);
    }

    private IEnumerable<string> ExpandPaths()
    {
        if (options.Paths.Count == 0)
            throw new UsageException("No path given. Run 'tcfmt --help'.");

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var path in options.Paths)
        {
            string full = Path.GetFullPath(path);
            IEnumerable<string> files;
            if (Directory.Exists(full))
            {
                files = Walk(full);
            }
            else if (File.Exists(full))
            {
                string ext = Path.GetExtension(full);
                if (IsSupported(full))
                    files = new[] { full };
                else if (ext.Equals(".plcproj", StringComparison.OrdinalIgnoreCase) ||
                         ext.Equals(".tsproj", StringComparison.OrdinalIgnoreCase) ||
                         ext.Equals(".sln", StringComparison.OrdinalIgnoreCase))
                    files = Walk(Path.GetDirectoryName(full)!);
                else
                    throw new UsageException($"'{path}' is not a TwinCAT source file, folder or project.");
            }
            else
            {
                throw new UsageException($"'{path}' does not exist.");
            }

            foreach (var file in files.OrderBy(f => f, StringComparer.OrdinalIgnoreCase))
            {
                if (seen.Add(file))
                    yield return file;
            }
        }
    }

    private static bool IsSupported(string file) =>
        TcFileProcessor.Extensions.Any(e => file.EndsWith(e, StringComparison.OrdinalIgnoreCase));

    private static IEnumerable<string> Walk(string directory)
    {
        foreach (var file in Directory.EnumerateFiles(directory))
        {
            if (IsSupported(file))
                yield return file;
        }
        foreach (var sub in Directory.EnumerateDirectories(directory))
        {
            if (SkippedFolders.Contains(Path.GetFileName(sub)))
                continue;
            foreach (var file in Walk(sub))
                yield return file;
        }
    }
}
