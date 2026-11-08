using System;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows.Threading;
using EnvDTE80;
using Microsoft.VisualStudio.Shell;
using TcFormatter.Configuration;
using TcFormatter.Vsix.Options;
using TcFormatter.Vsix.TwinCat;
using TcFormatter.Vsix.Ui;
using TCatSysManagerLib;

namespace TcFormatter.Vsix.Commands
{
    /// <summary>
    /// Menu commands. All commands are also available as DTE commands, e.g.
    /// <c>DTE.ExecuteCommand("TcFormatter.FormatSelection", "TIPC^Plc1^Plc1 Project^POUs")</c>.
    /// </summary>
    internal sealed class TcFormatterCommands
    {
        public static readonly Guid CommandSet = new Guid("4e7a1c2b-8d3f-4a5e-b6c7-1f2e3d4c5b6a");

        private const int FormatSelectionId = 0x0100;
        private const int FormatDocumentId = 0x0101;
        private const int CheckSelectionId = 0x0102;
        private const int FormatSolutionId = 0x0103;
        private const int CheckSolutionId = 0x0104;
        private const int OpenConfigId = 0x0105;
        private const int OpenOptionsId = 0x0106;
        private const int DiagnoseMenusId = 0x0107;
        private const int ImportStweepId = 0x0108;

        private readonly TcFormatterPackage _package;
        private readonly DTE2 _dte;
        private readonly OutputLog _log;
        private readonly NamingErrorList _errors;

        public TcFormatterCommands(TcFormatterPackage package, DTE2 dte, OutputLog log, NamingErrorList errors)
        {
            _package = package;
            _dte = dte;
            _log = log;
            _errors = errors;
        }

        public void Register(OleMenuCommandService service)
        {
            Add(service, FormatSelectionId, (arg, click) => Run(arg, click, recursive: true, format: true, lint: false));
            Add(service, CheckSelectionId, (arg, click) => Run(arg, click, recursive: true, format: false, lint: true));
            Add(service, FormatDocumentId, (arg, click) => RunActiveObject(click));
            Add(service, FormatSolutionId, (arg, click) => RunAll(format: true, lint: false));
            Add(service, CheckSolutionId, (arg, click) => RunAll(format: false, lint: true));
            Add(service, OpenConfigId, (arg, click) => OpenConfiguration());
            Add(service, OpenOptionsId, (arg, click) => _package.ShowOptionPage(typeof(FormatOptionsPage)));
            Add(service, ImportStweepId, (arg, click) => ImportStweep(arg));
            Add(service, DiagnoseMenusId, (arg, click) =>
            {
                string file = Path.Combine(Path.GetTempPath(), "TcFormatter-menus.txt");
                if (arg != null && arg.StartsWith("select:", StringComparison.OrdinalIgnoreCase))
                {
                    // select a Solution Explorer node (path of captions separated by '/') and report what the
                    // selection commands would process - simulates a right click on that node
                    // "select:<path>|format" or "|check" also runs the command on that selection (= right click + menu entry)
                    string spec = arg.Substring("select:".Length);
                    string action = null;
                    int bar = spec.LastIndexOf('|');
                    if (bar > 0)
                    {
                        action = spec.Substring(bar + 1).Trim().ToLowerInvariant();
                        spec = spec.Substring(0, bar);
                    }
                    string report = Ui.MenuDiagnostics.SelectAndDescribe(_dte, spec);
                    File.WriteAllText(file, report);
                    _log.Write(report);
                    if (action == "format" || action == "check")
                    {
                        var simulated = new ClickContext { Selection = PlcTree.Selection(_dte), ActiveObject = PlcTree.ActiveObject(_dte) };
                        Run(null, simulated, recursive: true, format: action == "format", lint: action == "check");
                    }
                    return;
                }
                File.WriteAllText(file, Ui.MenuDiagnostics.Dump(_dte, arg));
                _log.Write("Context menu report written to " + file);
            });
        }

        /// <summary>What was selected / active at the moment the command was invoked (e.g. the right-clicked node).</summary>
        private sealed class ClickContext
        {
            public List<ITcSmTreeItem> Selection = new List<ITcSmTreeItem>();
            public ITcSmTreeItem ActiveObject;
        }

        private void Add(OleMenuCommandService service, int id, Action<string, ClickContext> handler)
        {
            var command = new OleMenuCommand((sender, e) =>
            {
                ThreadHelper.ThrowIfNotOnUIThread();
                string argument = (e as OleMenuCmdEventArgs)?.InValue as string;
                argument = string.IsNullOrWhiteSpace(argument) ? null : argument.Trim().Trim('"');

                // capture the selection now: when the work runs later, the focus (and thus the selection)
                // may already have moved on to another window
                var click = new ClickContext();
                try
                {
                    click.Selection = PlcTree.Selection(_dte);
                    click.ActiveObject = PlcTree.ActiveObject(_dte);
                }
                catch (Exception)
                {
                    // the selection is resolved again when the command runs
                }

                // Run from the UI message loop instead of inside the command invocation: when the command is
                // executed through DTE.ExecuteCommand by another process, the call arrives as an incoming COM
                // call and the PLC engine refuses nested calls (SEHException). Also closes the menu immediately.
                Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
                {
                    ThreadHelper.ThrowIfNotOnUIThread();
                    try
                    {
                        handler(argument, click);
                    }
                    catch (Exception ex)
                    {
                        _log.Write("Unexpected error: " + ex);
                        _log.Show();
                        VsShellUtilities.ShowMessageBox(_package, ex.Message, "TcFormatter",
                            Microsoft.VisualStudio.Shell.Interop.OLEMSGICON.OLEMSGICON_CRITICAL,
                            Microsoft.VisualStudio.Shell.Interop.OLEMSGBUTTON.OLEMSGBUTTON_OK,
                            Microsoft.VisualStudio.Shell.Interop.OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST);
                    }
                }));
            }, new CommandID(CommandSet, id));
            command.ParametersDescription = "$";
            service.AddCommand(command);
        }

        // ------------------------------------------------------------------ command handlers

        private void Run(string treePath, ClickContext click, bool recursive, bool format, bool lint)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            List<ITcSmTreeItem> roots;
            if (treePath != null)
            {
                var item = PlcTree.Lookup(_dte, treePath);
                if (item == null)
                {
                    Report($"Tree item '{treePath}' not found.");
                    return;
                }
                roots = new List<ITcSmTreeItem> { item };
            }
            else
            {
                roots = click.Selection.Count > 0 ? click.Selection : PlcTree.Selection(_dte);
                if (roots.Count == 0)
                {
                    var active = click.ActiveObject ?? PlcTree.ActiveObject(_dte);
                    if (active != null)
                        roots.Add(active);
                }
            }

            if (roots.Count == 0)
            {
                Report("Select a PLC object, folder or PLC project in the Solution Explorer first.");
                return;
            }
            Execute(roots, recursive, format, lint, "selection: " + PlcTree.Describe(roots));
        }

        private void RunActiveObject(ClickContext click)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            var active = click.ActiveObject ?? PlcTree.ActiveObject(_dte);
            if (active == null)
            {
                Run(null, click, recursive: false, format: true, lint: false);
                return;
            }
            Execute(new List<ITcSmTreeItem> { active }, recursive: false, format: true, lint: false, PlcTree.Describe(new[] { active }));
        }

        private void RunAll(bool format, bool lint)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            var roots = PlcTree.PlcRoots(_dte).ToList();
            if (roots.Count == 0)
            {
                Report("The solution contains no TwinCAT PLC project.");
                return;
            }
            Execute(roots, recursive: true, format, lint, "all PLC projects");
        }

        private void Execute(List<ITcSmTreeItem> roots, bool recursive, bool format, bool lint, string what)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            var watch = Stopwatch.StartNew();
            string action = format ? "Formatting" : "Checking naming of";
            _log.Write($"{action} {what} ...");

            if (format)
            {
                // editors with unsaved changes would otherwise be overwritten
                try
                {
                    _dte.Documents.SaveAll();
                }
                catch
                {
                    // nothing to save or not possible
                }
            }

            var summary = new ProcessSummary();
            var configs = new Dictionary<string, TcFormatConfig>(StringComparer.OrdinalIgnoreCase);
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var statusBar = _dte.StatusBar;

            foreach (var root in roots)
            {
                foreach (var item in PlcTree.CodeObjectsBelow(root, recursive))
                {
                    string path;
                    try
                    {
                        path = item.PathName;
                    }
                    catch
                    {
                        continue;
                    }
                    if (!seen.Add(path))
                        continue;

                    statusBar.Text = $"TcFormatter: {path}";
                    PlcCodeProcessor.Process(item, ConfigFor(path, configs), format, lint, summary);
                }
            }

            foreach (var changed in summary.ChangedObjects)
                _log.Write("  formatted " + changed);
            foreach (var error in summary.Errors)
                _log.Write("  ERROR " + error);

            if (lint)
            {
                _errors.Show(summary.Naming, _dte);
                foreach (var issue in summary.Naming)
                    _log.Write($"  {issue.Diagnostic.Severity} {issue.Diagnostic.RuleId} {issue.TreePath} ({issue.Part} {issue.Diagnostic.Line},{issue.Diagnostic.Column}): {issue.Diagnostic.Message}");
            }

            string result = format
                ? $"{summary.Changed} of {summary.Objects} object(s) reformatted"
                : $"{summary.Naming.Count} naming issue(s) in {summary.Objects} object(s)";
            if (summary.Errors.Count > 0)
                result += $", {summary.Errors.Count} error(s)";
            result += $" ({watch.ElapsedMilliseconds} ms, access: {PlcText.LastAccessPath}).";

            _log.Write(result);
            statusBar.Text = "TcFormatter: " + result;
            if (summary.Errors.Count > 0)
                _log.Show();
        }

        private TcFormatConfig ConfigFor(string treePath, Dictionary<string, TcFormatConfig> cache)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            string directory = PlcTree.ObjectDirectory(_dte, treePath) ?? string.Empty;
            if (cache.TryGetValue(directory, out var config))
                return config;
            try
            {
                config = TcFormatConfig.LoadFor(directory, out string source, out var warnings);
                _log.Write("  configuration: " + (source ?? "built-in defaults"));
                foreach (var warning in warnings)
                    _log.Write("  WARNING " + warning);
            }
            catch (ConfigException ex)
            {
                _log.Write("  ERROR " + ex.Message + " - using built-in defaults.");
                _log.Show();
                config = new TcFormatConfig();
            }
            cache[directory] = config;
            return config;
        }

        private void OpenConfiguration()
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            string solution = _dte.Solution?.FullName;
            if (string.IsNullOrEmpty(solution))
            {
                Report("Open a solution first. Without a solution the settings from Tools > Options > TcFormatter are used.");
                return;
            }
            string file = Path.Combine(Path.GetDirectoryName(solution), TcFormatConfig.FileName);
            if (!File.Exists(file))
            {
                // start from the current IDE settings
                TcFormatConfig.LoadUser(out _).SaveTo(file);
                _log.Write("Created " + file);
            }
            _dte.ItemOperations.OpenFile(file);
        }

        /// <summary>
        /// Converts a settings file exported from STweep into the IDE settings (%APPDATA%\TcFormatter\.tcformat.json).
        /// The naming rules of the current settings are kept. Argument: path of the .stweep file (else a file dialog).
        /// </summary>
        private void ImportStweep(string file)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            if (file == null)
            {
                using (var dialog = new System.Windows.Forms.OpenFileDialog
                {
                    Title = "Import STweep settings",
                    Filter = "STweep settings (*.stweep)|*.stweep|All files (*.*)|*.*",
                })
                {
                    if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK)
                        return;
                    file = dialog.FileName;
                }
            }

            TcFormatConfig imported;
            List<string> notes;
            try
            {
                imported = StweepImport.Convert(File.ReadAllText(file), out notes);
            }
            catch (Exception ex) when (ex is ConfigException || ex is IOException)
            {
                Report("STweep import failed: " + ex.Message);
                return;
            }

            var current = TcFormatConfig.LoadUser(out _);
            current.Format = imported.Format;
            current.SaveTo(TcFormatConfig.UserConfigFile);

            _log.Write($"Imported STweep settings from {file} into {TcFormatConfig.UserConfigFile}.");
            foreach (var note in notes)
                _log.Write("  NOTE " + note);
            _log.Write("A .tcformat.json in the project still takes precedence over these IDE settings.");
            _log.Show();
            _dte.StatusBar.Text = $"TcFormatter: STweep settings imported ({notes.Count} note(s), see Output window).";
        }

        private void Report(string message)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            _log.Write(message);
            _dte.StatusBar.Text = "TcFormatter: " + message;
            _log.Show();
        }
    }
}
