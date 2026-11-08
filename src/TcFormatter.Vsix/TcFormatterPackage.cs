using System;
using System.ComponentModel.Design;
using System.Runtime.InteropServices;
using System.Threading;
using EnvDTE80;
using Microsoft.VisualStudio.Shell;
using TcFormatter.Vsix.Commands;
using TcFormatter.Vsix.Options;
using TcFormatter.Vsix.Ui;
using Task = System.Threading.Tasks.Task;

namespace TcFormatter.Vsix
{
    /// <summary>
    /// TcFormatter extension for TwinCAT XAE (TcXaeShell 4024 / 4026 and Visual Studio with TwinCAT integration).
    /// </summary>
    [PackageRegistration(UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]
    [InstalledProductRegistration("TcFormatter", "Code formatter and naming checker for TwinCAT 3 Structured Text", "1.0.0")]
    [ProvideMenuResource("Menus.ctmenu", 1)]
    [ProvideOptionPage(typeof(FormatOptionsPage), "TcFormatter", "Formatting", 0, 0, true)]
    [ProvideOptionPage(typeof(NamingOptionsPage), "TcFormatter", "Naming", 0, 0, true)]
    [Guid(PackageGuidString)]
    public sealed class TcFormatterPackage : AsyncPackage
    {
        public const string PackageGuidString = "9c3f2e41-5b7a-4d6e-8f10-2a4b6c8d0e12";

        internal OutputLog Log { get; private set; }

        internal NamingErrorList ErrorList { get; private set; }

        protected override async Task InitializeAsync(CancellationToken cancellationToken, IProgress<ServiceProgressData> progress)
        {
            await JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

            var commandService = (OleMenuCommandService)await GetServiceAsync(typeof(IMenuCommandService));
            var dte = (DTE2)await GetServiceAsync(typeof(EnvDTE.DTE));

            Log = new OutputLog(this);
            ErrorList = new NamingErrorList(this, dte);
            new TcFormatterCommands(this, dte, Log, ErrorList).Register(commandService);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                ErrorList?.Dispose();
            base.Dispose(disposing);
        }
    }
}
