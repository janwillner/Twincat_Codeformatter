using System;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.Diagnostics;
using System.IO;
using System.Linq;
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
            Add(service, FormatSelectionId, arg => Run(arg, recursive: true, format: true, lint: false, selection: true));
            Add(service, CheckSelectionId, arg => Run(arg, recursive: true, format: false, lint: true, selection: true));
            Add(service, FormatDocumentId, arg => RunActiveObject());
            Add(service, FormatSolutionId, arg => RunAll(format: true, lint: false));
            Add(service, CheckSolutionId, arg => RunAll(format: false, lint: true));
            Add(service, OpenConfigId, arg => OpenConfiguration());
            Add(service, OpenOptionsId, arg => _package.ShowOptionPage(typeof(FormatOptionsPage)));
        }

        private void Add(OleMenuCommandService service, int id, Action<string> handler)
        {
            var command = new OleMenuCommand((sender, e) =>
            {
                ThreadHelper.ThrowIfNotOnUIThread();
                string argument = (e as OleMenuCmdEventArgs)?.InValue as string;
                try
                {
                    handler(string.IsNullOrWhiteSpace(argument) ? null : argument.Trim().Trim('"'));
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
            }, new CommandID(CommandSet, id));
            command.ParametersDescription = "$";
            service.AddCommand(command);
        }

        // ------------------------------------------------------------------ command handlers

        private void Run(string treePath, bool recursive, bool format, bool lint, bool selection)
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
                roots = selection ? PlcTree.Selection(_dte) : new List<ITcSmTreeItem>();
                if (roots.Count == 0)
                {
                    var active = PlcTree.ActiveObject(_dte);
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

        private void RunActiveObject()
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            var active = PlcTree.ActiveObject(_dte);
            if (active == null)
            {
                Run(null, recursive: false, format: true, lint: false, selection: true);
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
            result += $" ({watch.ElapsedMilliseconds} ms).";

            _log.Write(result);
            statusBar.Text = "TcFormatter: " + result;
            if (summary.Errors.Count > 0)
                _log.Show();
        }

        private TcFormatConfig ConfigFor(string treePath, Dictionary<string, TcFormatConfig> cache)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            string directory = PlcTree.ProjectDirectory(_dte, treePath) ?? string.Empty;
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

        private void Report(string message)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            _log.Write(message);
            _dte.StatusBar.Text = "TcFormatter: " + message;
            _log.Show();
        }
    }
}
