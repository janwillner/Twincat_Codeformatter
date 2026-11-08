using System;
using System.IO;
using EnvDTE80;
using Microsoft.VisualStudio.Shell;
using TcFormatter.Configuration;

namespace TcFormatter.Vsix.Options
{
    /// <summary>
    /// The settings file that Tools > Options, Import and Export work on: the .tcformat.json that applies to the
    /// open solution (it takes precedence when formatting), otherwise the IDE settings in %APPDATA%.
    /// </summary>
    internal sealed class SettingsTarget
    {
        private SettingsTarget(string file, bool isProjectFile)
        {
            File = file;
            IsProjectFile = isProjectFile;
        }

        public string File { get; }

        /// <summary>True for a .tcformat.json of the open solution, false for the IDE settings.</summary>
        public bool IsProjectFile { get; }

        public static SettingsTarget Resolve()
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            string solution = null;
            try
            {
                var dte = Package.GetGlobalService(typeof(EnvDTE.DTE)) as DTE2;
                solution = dte?.Solution?.FullName;
            }
            catch (Exception)
            {
                // no DTE (e.g. while the shell starts): IDE settings
            }

            if (!string.IsNullOrEmpty(solution))
            {
                string project = TcFormatConfig.FindFile(Path.GetDirectoryName(solution));
                if (project != null)
                    return new SettingsTarget(project, true);
            }
            return new SettingsTarget(TcFormatConfig.UserConfigFile, false);
        }

        /// <summary>Reads the file (defaults when it does not exist yet).</summary>
        /// <exception cref="ConfigException">The file is no valid configuration.</exception>
        public TcFormatConfig Load() =>
            System.IO.File.Exists(File) ? TcFormatConfig.Load(File, out _) : new TcFormatConfig();

        /// <summary>Time stamp of the file, to notice changes made outside of the options page.</summary>
        public DateTime Stamp => System.IO.File.Exists(File) ? System.IO.File.GetLastWriteTimeUtc(File) : DateTime.MinValue;

        public string Caption => IsProjectFile
            ? "Project settings of the open solution: " + File
            : "IDE settings (used when the solution has no .tcformat.json): " + File;
    }
}
