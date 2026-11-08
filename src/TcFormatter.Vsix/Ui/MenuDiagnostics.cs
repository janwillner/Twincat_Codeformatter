using System;
using System.Collections.Generic;
using System.Text;
using EnvDTE80;
using Microsoft.VisualStudio.Shell;

namespace TcFormatter.Vsix.Ui
{
    /// <summary>
    /// Lists the shell's context menus (command bars of type popup) with their entries. Used to find out which
    /// context menus TwinCAT shows for PLC objects, folders and projects. Run "TcFormatter.DiagnoseMenus"
    /// in the Command Window (optional argument: text that a menu entry must contain).
    /// </summary>
    internal static class MenuDiagnostics
    {
        /// <summary>Selects the Solution Explorer node at <paramref name="captionPath"/> ("A/B/C") and describes the selection.</summary>
        public static string SelectAndDescribe(DTE2 dte, string captionPath)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            var parts = captionPath.Split('/');
            EnvDTE.UIHierarchy explorer = dte.ToolWindows.SolutionExplorer;
            EnvDTE.UIHierarchyItems items = explorer.UIHierarchyItems;
            EnvDTE.UIHierarchyItem found = null;
            foreach (var part in parts)
            {
                found = null;
                foreach (EnvDTE.UIHierarchyItem item in items)
                {
                    if (string.Equals(item.Name, part, StringComparison.OrdinalIgnoreCase))
                    {
                        found = item;
                        break;
                    }
                }
                if (found == null)
                {
                    var names = new List<string>();
                    foreach (EnvDTE.UIHierarchyItem item in items)
                        names.Add(item.Name);
                    return $"Node '{part}' of '{captionPath}' not found. Available: {string.Join(" | ", names)}";
                }
                found.UIHierarchyItems.Expanded = true;
                items = found.UIHierarchyItems;
            }
            found.Select(EnvDTE.vsUISelectionType.vsUISelectionTypeSelect);
            explorer.Parent.Activate(); // like a right click: the Solution Explorer becomes the active window

            var selection = TwinCat.PlcTree.Selection(dte);
            return $"Selected '{captionPath}' -> {selection.Count} PLC tree item(s): {TwinCat.PlcTree.Describe(selection)}";
        }

        public static string Dump(DTE2 dte, string filter)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            var sb = new StringBuilder();
            dynamic bars = dte.CommandBars;
            int count = bars.Count;
            sb.AppendLine($"{count} command bars");
            for (int i = 1; i <= count; i++)
            {
                dynamic bar;
                string name;
                int type;
                try
                {
                    bar = bars[i];
                    name = bar.Name;
                    type = (int)bar.Type;
                }
                catch (Exception ex)
                {
                    sb.AppendLine($"#{i}: <{ex.Message}>");
                    continue;
                }
                if (type != 2) // msoBarTypePopup
                    continue;

                var entries = new StringBuilder();
                bool match = string.IsNullOrEmpty(filter);
                try
                {
                    dynamic controls = bar.Controls;
                    int n = controls.Count;
                    for (int c = 1; c <= n; c++)
                    {
                        string caption = controls[c].Caption;
                        entries.Append(" | ").Append(caption);
                        if (!match && caption != null && caption.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
                            match = true;
                    }
                }
                catch (Exception ex)
                {
                    entries.Append(" <" + ex.Message + ">");
                }
                if (match)
                    sb.AppendLine($"#{i} [{name}]{entries}");
            }
            return sb.ToString();
        }
    }
}
