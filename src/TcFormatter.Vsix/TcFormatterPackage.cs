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
    // load in the background at start so the sub menus in TwinCAT's own context menus are available right away
    [ProvideAutoLoad("ADFC4E64-0397-11D1-9F4E-00A0C911004F", PackageAutoLoadFlags.BackgroundLoad)] // UIContextGuids80.NoSolution
    [ProvideAutoLoad("f1536ef8-92ec-443c-9ed7-fdadf150da82", PackageAutoLoadFlags.BackgroundLoad)] // UIContextGuids80.SolutionExists
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

            try
            {
                var menus = PlcContextMenus.Attach(dte);
                Log.Write("TcFormatter context menu added to: " + (menus.Count == 0 ? "(no TwinCAT PLC menus found)" : string.Join(", ", menus)));
            }
            catch (Exception ex)
            {
                Log.Write("Could not add the TcFormatter context menu to the TwinCAT PLC menus: " + ex.Message);
            }
        }

        /// <summary>Re-reads the option pages after the settings file was changed outside of Tools > Options.</summary>
        internal void ReloadOptionPages()
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            GetDialogPage(typeof(FormatOptionsPage)).LoadSettingsFromStorage();
            GetDialogPage(typeof(NamingOptionsPage)).LoadSettingsFromStorage();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                ErrorList?.Dispose();
            base.Dispose(disposing);
        }
    }
}
