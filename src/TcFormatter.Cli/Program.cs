using System.Text;
using TcFormatter.Configuration;
using TcFormatter.Formatting;
using TcFormatter.Naming;
using TcFormatter.TwinCatFiles;

namespace TcFormatter.Cli;

public static class Program
{
    public const int Ok = 0;
    public const int CheckFailed = 1;
    public const int UsageError = 2;
    public const int FormatterError = 3;

    private const string Help = """
        tcfmt - formatter and naming checker for TwinCAT 3 Structured Text

        Usage:
          tcfmt format [options] <path>...   Format .TcPOU/.TcDUT/.TcGVL/.TcIO files, folders, .plcproj/.tsproj/.sln
          tcfmt check  [options] <path>...   Like 'format --check': exit code 1 when files would change
          tcfmt lint   [options] <path>...   Check naming conventions
          tcfmt stdin  --part decl|impl      Format Structured Text from stdin to stdout
          tcfmt init   [<directory>]         Write a .tcformat.json with all options and their defaults
          tcfmt import-stweep <file.stweep> [<directory>]
                                             Convert settings exported from STweep into a .tcformat.json
          tcfmt explain [<option>]           List all options, or show a before/after example of one option
                                             with the current configuration and its other values

        Options:
          --config <file>      Use this configuration instead of the nearest .tcformat.json
          --no-user-config     Ignore %APPDATA%\TcFormatter\.tcformat.json (IDE settings) when no project file exists
          --check              Do not write files; exit code 1 when a file would be reformatted
          --diff               Print a diff of the changes (implies --check unless --write is given)
          --write              Write files even with --diff
          --lint               'format': also check naming
          --fail-on <level>    'lint': exit code 1 at this severity or higher: error (default), warning, info, none
          --json               'lint': print diagnostics as JSON lines
          --part <decl|impl>   'stdin': which part of the object the text is
          --path <file>        'stdin': file path used to find the .tcformat.json
          --force              'init': overwrite an existing file
          -q, --quiet          Only print errors and diagnostics
          --version            Print the version
        """;

    public static int Main(string[] args)
    {
        // the examples show tabs as '→'
        if (args.Length > 0 && args[0] == "explain")
        {
            try { Console.OutputEncoding = new UTF8Encoding(false); }
            catch (IOException) { }
        }
        return Run(args, Console.In, Console.Out, Console.Error);
    }

    public static int Run(string[] args, TextReader stdin, TextWriter stdout, TextWriter stderr)
    {
        try
        {
            var options = CliOptions.Parse(args);
            if (options.ShowVersion)
            {
                stdout.WriteLine("tcfmt " + typeof(StFormatter).Assembly.GetName().Version);
                return Ok;
            }
            if (options.ShowHelp || options.Command == null)
            {
                stdout.WriteLine(Help);
                return options.Command == null && !options.ShowHelp ? UsageError : Ok;
            }

            return options.Command switch
            {
                "format" => new Runner(options, stdout, stderr).Format(),
                "check" => new Runner(options with { Check = true }, stdout, stderr).Format(),
                "lint" => new Runner(options, stdout, stderr).Lint(),
                "stdin" => Stdin(options, stdin, stdout, stderr),
                "init" => Init(options, stdout, stderr),
                "import-stweep" => ImportStweep(options, stdout, stderr),
                "explain" => Explain(options, stdout, stderr),
                _ => throw new UsageException($"Unknown command '{options.Command}'. Run 'tcfmt --help'."),
            };
        }
        catch (UsageException ex)
        {
            stderr.WriteLine("error: " + ex.Message);
            return UsageError;
        }
        catch (ConfigException ex)
        {
            stderr.WriteLine("error: " + ex.Message);
            return UsageError;
        }
    }

    private static int Stdin(CliOptions options, TextReader stdin, TextWriter stdout, TextWriter stderr)
    {
        var part = options.Part switch
        {
            "decl" or "declaration" => CodePart.Declaration,
            "impl" or "implementation" => CodePart.Implementation,
            _ => throw new UsageException("'stdin' needs --part decl or --part impl."),
        };
        string directory = (options.StdinPath != null ? Path.GetDirectoryName(Path.GetFullPath(options.StdinPath)) : null)
                           ?? Directory.GetCurrentDirectory();
        var config = LoadConfig(options, directory, stderr);
        var result = StFormatter.Format(stdin.ReadToEnd(), part, config.Format);
        stdout.Write(result.Text);
        if (result.Error != null)
        {
            stderr.WriteLine("error: " + result.Error);
            return FormatterError;
        }
        return Ok;
    }

    private static int Init(CliOptions options, TextWriter stdout, TextWriter stderr)
    {
        string directory = options.Paths.Count > 0 ? options.Paths[0] : Directory.GetCurrentDirectory();
        Directory.CreateDirectory(directory);
        string file = Path.Combine(directory, TcFormatConfig.FileName);
        if (File.Exists(file) && !options.Force)
        {
            stderr.WriteLine($"error: {file} already exists (use --force to overwrite).");
            return UsageError;
        }
        File.WriteAllText(file, new TcFormatConfig().ToJson(includeDescriptions: true), new UTF8Encoding(false));
        stdout.WriteLine("Created " + file);
        return Ok;
    }

    private static int ImportStweep(CliOptions options, TextWriter stdout, TextWriter stderr)
    {
        if (options.Paths.Count == 0)
            throw new UsageException("'import-stweep' needs the STweep settings file: tcfmt import-stweep <file.stweep> [<directory>]");
        string source = options.Paths[0];
        if (!File.Exists(source))
            throw new UsageException($"'{source}' does not exist.");
        string directory = options.Paths.Count > 1 ? options.Paths[1] : Directory.GetCurrentDirectory();
        string target = directory.EndsWith(".json", StringComparison.OrdinalIgnoreCase)
            ? directory
            : Path.Combine(directory, TcFormatConfig.FileName);
        if (File.Exists(target) && !options.Force)
        {
            stderr.WriteLine($"error: {target} already exists (use --force to overwrite).");
            return UsageError;
        }

        var config = StweepImport.Convert(File.ReadAllText(source), out var notes);
        config.SaveTo(target);
        stdout.WriteLine($"Created {target} from {source}.");
        if (notes.Count > 0)
        {
            stdout.WriteLine($"{notes.Count} STweep setting(s) could not be taken over exactly:");
            foreach (var note in notes)
                stdout.WriteLine("  - " + note);
        }
        return Ok;
    }

    private static int Explain(CliOptions options, TextWriter stdout, TextWriter stderr)
    {
        var config = LoadConfig(options, Directory.GetCurrentDirectory(), stderr);
        if (options.Paths.Count == 0)
        {
            stdout.WriteLine("format options:");
            foreach (var name in FormatterOptionExamples.Options)
                stdout.WriteLine($"  {Camel(name),-44} {FormatterOptionExamples.Description(name)}");
            stdout.WriteLine();
            stdout.WriteLine("naming options:");
            foreach (var name in NamingOptionExamples.Options)
                stdout.WriteLine($"  {Camel(name),-44} {NamingOptionExamples.Description(name)}");
            stdout.WriteLine();
            stdout.WriteLine("Run 'tcfmt explain <option>' for a before/after example.");
            return Ok;
        }

        // "format.indentSize", "naming.typePrefixes" or just the option name
        string option = options.Paths[0];
        int dot = option.IndexOf('.');
        string? section = dot > 0 ? option.Substring(0, dot).ToLowerInvariant() : null;
        if (dot > 0)
            option = option.Substring(dot + 1);

        if (section != "naming" && FormatterOptionExamples.Has(option))
        {
            var preview = FormatterOptionExamples.Preview(option, config.Format);
            WriteExample(stdout, "format." + Camel(option), FormatterOptionExamples.Description(option),
                CurrentValue(config.Format, option), preview);
            foreach (var (label, alternative) in FormatterOptionExamples.DescribeAlternatives(option, config.Format))
                WriteAlternative(stdout, label, FormatterOptionExamples.Preview(option, alternative).After);
            return Ok;
        }
        if (section != "format" && NamingOptionExamples.Has(option))
        {
            var preview = NamingOptionExamples.Preview(option, config.Naming);
            WriteExample(stdout, "naming." + Camel(option), NamingOptionExamples.Description(option),
                CurrentValue(config.Naming, option), preview);
            foreach (var (label, alternative) in NamingOptionExamples.DescribeAlternatives(option, config.Naming))
                WriteAlternative(stdout, label, NamingOptionExamples.Preview(option, alternative).After);
            return Ok;
        }
        throw new UsageException($"Unknown option '{options.Paths[0]}'. Run 'tcfmt explain' for the list.");
    }

    private static void WriteExample(TextWriter stdout, string name, string description, string value, OptionPreview preview)
    {
        stdout.WriteLine($"{name} = {value}");
        stdout.WriteLine("  " + description);
        stdout.WriteLine();
        stdout.WriteLine("Before:");
        stdout.WriteLine(Indent(preview.Before));
        stdout.WriteLine();
        stdout.WriteLine("After (current setting):");
        stdout.WriteLine(Indent(preview.After));
        if (preview.Error != null)
            stdout.WriteLine("  error: " + preview.Error);
    }

    private static void WriteAlternative(TextWriter stdout, string label, string after)
    {
        stdout.WriteLine();
        stdout.WriteLine($"After with {label}:");
        stdout.WriteLine(Indent(after));
    }

    private static string Indent(string text) =>
        string.Join(Environment.NewLine, text.Replace("\r\n", "\n").Split('\n').Select(line => "  | " + line));

    private static string CurrentValue(object options, string option)
    {
        var value = options.GetType().GetProperty(option, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.IgnoreCase)?.GetValue(options);
        return value switch
        {
            null => "null",
            string s => "\"" + s + "\"",
            System.Collections.IDictionary d => "{ " + string.Join(", ", d.Keys.Cast<object>().Take(6).Select(k => $"{k}: {d[k]}")) + (d.Count > 6 ? ", ..." : "") + " }",
            System.Collections.IEnumerable e => "[ " + string.Join(", ", e.Cast<object>()) + " ]",
            _ => value.ToString() ?? string.Empty,
        };
    }

    private static string Camel(string name) => char.ToLowerInvariant(name[0]) + name.Substring(1);

    internal static TcFormatConfig LoadConfig(CliOptions options, string directory, TextWriter stderr)
    {
        IReadOnlyList<string> warnings;
        TcFormatConfig config;
        if (options.ConfigFile != null)
        {
            if (!File.Exists(options.ConfigFile))
                throw new UsageException($"Configuration file '{options.ConfigFile}' not found.");
            config = TcFormatConfig.Load(options.ConfigFile, out warnings);
        }
        else
        {
            config = TcFormatConfig.LoadFor(directory, out _, out warnings, includeUserConfig: !options.NoUserConfig);
        }
        foreach (var warning in warnings)
            stderr.WriteLine("warning: " + warning);
        return config;
    }
}

internal sealed class UsageException(string message) : Exception(message);

internal sealed record CliOptions
{
    public string? Command { get; init; }
    public List<string> Paths { get; init; } = new();
    public string? ConfigFile { get; init; }
    public bool Check { get; init; }
    public bool Diff { get; init; }
    public bool Write { get; init; }
    public bool AlsoLint { get; init; }
    public NamingSeverity FailOn { get; init; } = NamingSeverity.Error;
    public bool FailOnNone { get; init; }
    public bool Json { get; init; }
    public string? Part { get; init; }
    public string? StdinPath { get; init; }
    public bool Force { get; init; }
    public bool Quiet { get; init; }
    public bool NoUserConfig { get; init; }
    public bool ShowHelp { get; init; }
    public bool ShowVersion { get; init; }

    public static CliOptions Parse(string[] args)
    {
        var o = new CliOptions();
        string Next(ref int i, string name) =>
            ++i < args.Length ? args[i] : throw new UsageException($"Option {name} needs a value.");

        for (int i = 0; i < args.Length; i++)
        {
            string a = args[i];
            switch (a)
            {
                case "-h": case "--help": o = o with { ShowHelp = true }; break;
                case "--version": o = o with { ShowVersion = true }; break;
                case "--config": o = o with { ConfigFile = Next(ref i, a) }; break;
                case "--check": o = o with { Check = true }; break;
                case "--diff": o = o with { Diff = true }; break;
                case "--write": o = o with { Write = true }; break;
                case "--lint": o = o with { AlsoLint = true }; break;
                case "--json": o = o with { Json = true }; break;
                case "--force": o = o with { Force = true }; break;
                case "-q": case "--quiet": o = o with { Quiet = true }; break;
                case "--no-user-config": o = o with { NoUserConfig = true }; break;
                case "--part": o = o with { Part = Next(ref i, a).ToLowerInvariant() }; break;
                case "--path": o = o with { StdinPath = Next(ref i, a) }; break;
                case "--fail-on":
                    string level = Next(ref i, a).ToLowerInvariant();
                    o = level switch
                    {
                        "none" => o with { FailOnNone = true },
                        "error" => o with { FailOn = NamingSeverity.Error },
                        "warning" => o with { FailOn = NamingSeverity.Warning },
                        "info" => o with { FailOn = NamingSeverity.Info },
                        _ => throw new UsageException("--fail-on must be error, warning, info or none."),
                    };
                    break;
                default:
                    if (a.StartsWith("-", StringComparison.Ordinal))
                        throw new UsageException($"Unknown option '{a}'.");
                    if (o.Command == null)
                        o = o with { Command = a.ToLowerInvariant() };
                    else
                        o.Paths.Add(a);
                    break;
            }
        }
        return o;
    }
}
