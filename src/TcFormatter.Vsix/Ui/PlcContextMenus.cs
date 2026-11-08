using System;
using System.Collections.Generic;
using System.Reflection;
using EnvDTE;
using EnvDTE80;
using Microsoft.VisualStudio.Shell;

namespace TcFormatter.Vsix.Ui
{
    /// <summary>
    /// Adds a "TcFormatter" sub menu to TwinCAT's own context menus of the PLC tree (POUs/DUTs/GVLs, folders,
    /// PLC projects) and of the PLC code editor.
    /// </summary>
    /// <remarks>
    /// TwinCAT does not use the standard Solution Explorer context menus (IDM_VS_CTXT_ITEMNODE, ...) for PLC
    /// nodes but its own command bars ("PlcFile", "PlcFolder", ...), whose GUID/ID are not public. They are
    /// therefore extended at runtime through the CommandBars automation model with a temporary popup, which
    /// works the same way in TcXaeShell 4024 (VS 2017/2019) and 4026 (VS 2022). The popup is not persisted and
    /// is re-created every time the extension loads.
    /// </remarks>
    internal static class PlcContextMenus
    {
        private const int PopupBarType = 2;       // msoBarTypePopup
        private const int PopupControlType = 10;  // msoControlPopup
        private const string Caption = "TcFormatter";

        /// <summary>Context menu name, and an entry it must contain (to tell apart menus with the same name).</summary>
        private static readonly (string Bar, string Contains, string[] Commands)[] Targets =
        {
            ("PlcFile", null, TreeCommands),
            ("PlcFolder", null, TreeCommands),
            ("PlcProject", "Check all objects", TreeCommands),
            ("Projects", "Activate Boot Project", TreeCommands),
            ("Plc2ParentPrj", null, TreeCommands),
            ("PlcCodeWinContextMenu", null, EditorCommands),
        };

        private static string[] TreeCommands => new[]
        {
            "TcFormatter.FormatSelection",
            "TcFormatter.CheckNamingSelection",
        };

        private static string[] EditorCommands => new[]
        {
            "TcFormatter.FormatCurrentObject",
            "TcFormatter.CheckNamingSelection",
        };

        /// <summary>Returns the names of the context menus that were extended.</summary>
        public static List<string> Attach(DTE2 dte)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            var attached = new List<string>();
            dynamic bars = dte.CommandBars;
            int count = bars.Count;
            for (int i = 1; i <= count; i++)
            {
                try
                {
                    dynamic bar = bars[i];
                    if ((int)bar.Type != PopupBarType)
                        continue;
                    string name = bar.Name;
                    foreach (var target in Targets)
                    {
                        if (!string.Equals(name, target.Bar, StringComparison.Ordinal))
                            continue;
                        if (HasEntry(bar, Caption) || (target.Contains != null && !HasEntry(bar, target.Contains)))
                            continue;
                        AddPopup(dte, bar, target.Commands);
                        attached.Add(name);
                    }
                }
                catch (Exception)
                {
                    // a single inaccessible command bar must not prevent the others
                }
            }
            return attached;
        }

        private static void AddPopup(DTE2 dte, dynamic bar, string[] commands)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            object missing = Missing.Value;
            dynamic popup = bar.Controls.Add(PopupControlType, missing, missing, missing, true);
            popup.Caption = Caption;
            popup.BeginGroup = true;
            object popupBar = popup.CommandBar;

            int position = 1;
            foreach (var commandName in commands)
            {
                Command command = dte.Commands.Item(commandName, -1);
                command.AddControl(popupBar, position++);
            }
        }

        private static bool HasEntry(dynamic bar, string text)
        {
            dynamic controls = bar.Controls;
            int count = controls.Count;
            for (int c = 1; c <= count; c++)
            {
                string caption = controls[c].Caption;
                if (caption != null && caption.IndexOf(text, StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            }
            return false;
        }
    }
}
