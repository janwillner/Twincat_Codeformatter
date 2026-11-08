using System;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;

namespace TcFormatter.Vsix.Ui
{
    /// <summary>"TcFormatter" pane in the Output window.</summary>
    internal sealed class OutputLog
    {
        private static readonly Guid PaneGuid = new Guid("b1e2c3d4-5f60-4a7b-8c9d-0e1f2a3b4c5d");
        private readonly IServiceProvider _services;
        private IVsOutputWindowPane _pane;

        public OutputLog(IServiceProvider services) => _services = services;

        private IVsOutputWindowPane Pane
        {
            get
            {
                ThreadHelper.ThrowIfNotOnUIThread();
                if (_pane != null)
                    return _pane;
                if (_services.GetService(typeof(SVsOutputWindow)) is IVsOutputWindow window)
                {
                    Guid guid = PaneGuid;
                    window.CreatePane(ref guid, "TcFormatter", 1, 1);
                    window.GetPane(ref guid, out _pane);
                }
                return _pane;
            }
        }

        public void Write(string line)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            Pane?.OutputString(DateTime.Now.ToString("HH:mm:ss") + "  " + line + Environment.NewLine);
        }

        public void Show()
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            Pane?.Activate();
            if (_services.GetService(typeof(SVsUIShell)) is IVsUIShell shell)
            {
                Guid output = new Guid("34E76E81-EE4A-11D0-AE2E-00A0C90FFFC3"); // ToolWindowGuids80.Outputwindow
                if (shell.FindToolWindow((uint)__VSFINDTOOLWIN.FTW_fForceCreate, ref output, out var frame) == VSConstants.S_OK)
                    frame?.Show();
            }
        }
    }
}
