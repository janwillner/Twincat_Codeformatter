using System;
using System.Collections.Generic;
using EnvDTE80;
using Microsoft.VisualStudio.Shell;
using TcFormatter.Naming;
using TcFormatter.Vsix.TwinCat;

namespace TcFormatter.Vsix.Ui
{
    /// <summary>Shows naming issues in the Error List. Double click opens the PLC object.</summary>
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
                foreach (var issue in issues)
                {
                    var d = issue.Diagnostic;
                    if (d.Severity == NamingSeverity.Off)
                        continue;
                    var task = new ErrorTask
                    {
                        Category = TaskCategory.CodeSense,
                        ErrorCategory = d.Severity == NamingSeverity.Error ? TaskErrorCategory.Error
                            : d.Severity == NamingSeverity.Warning ? TaskErrorCategory.Warning
                            : TaskErrorCategory.Message,
                        Text = $"{d.RuleId}: {d.Message}",
                        Document = issue.TreePath,
                        Line = Math.Max(0, d.Line - 1),
                        Column = Math.Max(0, d.Column - 1),
                    };
                    string path = issue.TreePath;
                    task.Navigate += (sender, e) =>
                    {
                        ThreadHelper.ThrowIfNotOnUIThread();
                        PlcTree.Open(dte, path);
                    };
                    _provider.Tasks.Add(task);
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
