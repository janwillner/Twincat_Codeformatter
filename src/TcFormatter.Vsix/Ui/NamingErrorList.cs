using System;
using System.Collections.Generic;
using EnvDTE80;
using Microsoft.VisualStudio.Shell;
using TcFormatter.Naming;
using TcFormatter.Vsix.TwinCat;

namespace TcFormatter.Vsix.Ui
{
    /// <summary>
    /// Shows naming issues in the Error List. Double click opens the PLC object.
    /// </summary>
    /// <remarks>
    /// The base class of <see cref="ErrorTask"/> is "Microsoft.VisualStudio.Shell.Task" in VS 2017/2019
    /// (TcXaeShell 4024) but "TaskListItem" in VS 2022 (TcXaeShell 4026). Code compiled against one of them
    /// fails with a TypeLoadException in the other, so every member that is declared on that base class is
    /// accessed through <c>dynamic</c>.
    /// </remarks>
    internal sealed class NamingErrorList : IDisposable
    {
        private readonly ErrorListProvider _provider;

        public NamingErrorList(IServiceProvider services, DTE2 dte)
        {
            _provider = new ErrorListProvider(services)
            {
                ProviderName = "TcFormatter",
                ProviderGuid = new Guid("d7c6b5a4-9382-4f1e-a0b9-c8d7e6f5a4b3"),
            };
        }

        public void Show(IReadOnlyList<NamingIssue> issues, DTE2 dte)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            _provider.SuspendRefresh();
            try
            {
                _provider.Tasks.Clear();
                dynamic tasks = _provider.Tasks;
                foreach (var issue in issues)
                {
                    var d = issue.Diagnostic;
                    if (d.Severity == NamingSeverity.Off)
                        continue;

                    dynamic task = Activator.CreateInstance(typeof(ErrorTask));
                    task.Category = TaskCategory.CodeSense;
                    task.ErrorCategory = d.Severity == NamingSeverity.Error ? TaskErrorCategory.Error
                        : d.Severity == NamingSeverity.Warning ? TaskErrorCategory.Warning
                        : TaskErrorCategory.Message;
                    task.Text = $"{d.RuleId}: {d.Message}";
                    task.Document = issue.TreePath;
                    task.Line = Math.Max(0, d.Line - 1);
                    task.Column = Math.Max(0, d.Column - 1);

                    string path = issue.TreePath;
                    EventHandler navigate = (sender, e) =>
                    {
                        ThreadHelper.ThrowIfNotOnUIThread();
                        PlcTree.Open(dte, path);
                    };
                    task.Navigate += navigate;
                    tasks.Add(task);
                }
            }
            finally
            {
                _provider.ResumeRefresh();
            }
            if (issues.Count > 0)
                _provider.Show();
        }

        public void Dispose() => _provider.Dispose();
    }
}
