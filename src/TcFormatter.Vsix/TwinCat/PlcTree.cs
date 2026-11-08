using System;
using System.Collections.Generic;
using System.Linq;
using EnvDTE;
using EnvDTE80;
using Microsoft.VisualStudio.Shell;
using TCatSysManagerLib;

namespace TcFormatter.Vsix.TwinCat
{
    /// <summary>Access to the TwinCAT project tree through the Automation Interface (TCatSysManagerLib 3.3).</summary>
    internal static class PlcTree
    {
        // TREEITEMTYPES (identical in TwinCAT 3.1.4024 and 4026)
        public const int ProjectDef = 56;          // TIPC^<Plc>  (holds the nested PLC project)
        public const int PlcFolder = 601;
        public const int Action = 608;
        public const int Transition = 616;
        public const int Gvl = 615;
        public const int GvlParamList = 629;

        /// <summary>EnvDTE.Constants.vsViewKindPrimary (the Constants class cannot be embedded).</summary>
        private const string ViewKindPrimary = "{7651A701-06E5-11D1-8EBD-00A0C90F26EA}";

        /// <summary>Object types that carry a declaration and/or Structured Text implementation.</summary>
        private static readonly HashSet<int> CodeObjects = new HashSet<int>
        {
            602, 603, 604,           // program, function, function block
            605, 606, 607, 623,      // enum, struct, union, alias
            608, 609, 610,           // action, method, interface method
            611, 612, 613, 614,      // property, interface property, get, set
            615, 616, 618, 629,      // GVL, transition, interface, parameter list
            // 654 / 655 (interface property get / set) carry no code
        };

        /// <summary>Nodes that never contain user code and can be large (instances, libraries, tasks, visualisations, ...).</summary>
        private static readonly HashSet<int> Skipped = new HashSet<int>
        {
            7, 8, 57, 617, 619, 620, 621, 622, 624, 625, 626, 627, 628, 630, 631, 632, 633, 634, 635,
            650, 651, 652, 653, 656, 657, 658,
        };

        public static bool IsCodeObject(ITcSmTreeItem item) => CodeObjects.Contains(SafeType(item));

        public static int SafeType(ITcSmTreeItem item)
        {
            try
            {
                return item.ItemType;
            }
            catch
            {
                return -1;
            }
        }

        /// <summary>Enumerates the code objects below (and including) <paramref name="root"/>.</summary>
        public static IEnumerable<ITcSmTreeItem> CodeObjectsBelow(ITcSmTreeItem root, bool recursive)
        {
            var stack = new Stack<ITcSmTreeItem>();
            stack.Push(root);
            bool first = true;
            while (stack.Count > 0)
            {
                var item = stack.Pop();
                int type = SafeType(item);

                if (type == ProjectDef)
                {
                    var nested = (item as ITcProjectRoot)?.NestedProject;
                    if (nested != null)
                        stack.Push(nested);
                    first = false;
                    continue;
                }
                if (Skipped.Contains(type) && !first)
                    continue;

                if (CodeObjects.Contains(type))
                    yield return item;

                if (recursive || first)
                {
                    bool descend = recursive || !CodeObjects.Contains(type);
                    if (descend)
                    {
                        int count;
                        try
                        {
                            count = item.ChildCount;
                        }
                        catch
                        {
                            count = 0;
                        }
                        // push in reverse so that children are visited in tree order
                        for (int i = count; i >= 1; i--)
                        {
                            ITcSmTreeItem child = null;
                            try
                            {
                                child = item.get_Child(i);
                            }
                            catch
                            {
                                // ignore inaccessible children
                            }
                            if (child != null)
                                stack.Push(child);
                        }
                    }
                }
                first = false;
            }
        }

        /// <summary>All TwinCAT system managers (one per .tsproj) of the open solution.</summary>
        public static IEnumerable<ITcSysManager> SystemManagers(DTE2 dte)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            foreach (Project project in AllProjects(dte))
            {
                object obj = null;
                try
                {
                    obj = project.Object;
                }
                catch
                {
                    // unloaded project
                }
                if (obj is ITcSysManager manager)
                    yield return manager;
            }
        }

        /// <summary>The PLC root (TIPC) of every TwinCAT project.</summary>
        public static IEnumerable<ITcSmTreeItem> PlcRoots(DTE2 dte)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            foreach (var manager in SystemManagers(dte))
            {
                ITcSmTreeItem root = null;
                try
                {
                    root = manager.LookupTreeItem("TIPC");
                }
                catch
                {
                    // no PLC in this project
                }
                if (root != null)
                    yield return root;
            }
        }

        /// <summary>Resolves a tree path like "TIPC^Plc1^Plc1 Project^POUs^MAIN" in any TwinCAT project.</summary>
        public static ITcSmTreeItem Lookup(DTE2 dte, string treePath)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            foreach (var manager in SystemManagers(dte))
            {
                try
                {
                    var item = manager.LookupTreeItem(treePath);
                    if (item != null)
                        return item;
                }
                catch
                {
                    // not in this project
                }
            }
            return null;
        }

        /// <summary>Tree items for the current Solution Explorer selection.</summary>
        public static List<ITcSmTreeItem> Selection(DTE2 dte)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            var result = new List<ITcSmTreeItem>();
            try
            {
                foreach (SelectedItem selected in dte.SelectedItems)
                {
                    object obj = null;
                    try
                    {
                        obj = selected.ProjectItem?.Object ?? selected.Project?.Object;
                    }
                    catch
                    {
                        // item without automation object
                    }
                    if (obj is ITcSmTreeItem item)
                        result.Add(item);
                    else if (obj is ITcSysManager manager)
                        result.Add(manager.LookupTreeItem("TIPC"));
                }
            }
            catch
            {
                // no selection available
            }
            return result;
        }

        /// <summary>Tree item of the object shown in the active editor window.</summary>
        public static ITcSmTreeItem ActiveObject(DTE2 dte)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            try
            {
                var item = dte.ActiveWindow?.ProjectItem ?? dte.ActiveDocument?.ProjectItem;
                return item?.Object as ITcSmTreeItem;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>Opens the editor of a PLC object (best effort).</summary>
        public static bool Open(DTE2 dte, string treePath)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            foreach (Project project in AllProjects(dte))
            {
                var found = Find(project.ProjectItems, treePath);
                if (found != null)
                {
                    try
                    {
                        found.Open(ViewKindPrimary)?.Activate();
                        return true;
                    }
                    catch
                    {
                        return false;
                    }
                }
            }
            return false;
        }

        private static ProjectItem Find(ProjectItems items, string treePath)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            if (items == null)
                return null;
            foreach (ProjectItem item in items)
            {
                string path = null;
                try
                {
                    path = (item.Object as ITcSmTreeItem)?.PathName;
                }
                catch
                {
                    // ignore
                }
                if (path != null && string.Equals(path, treePath, StringComparison.OrdinalIgnoreCase))
                    return item;
                if (path != null && !treePath.StartsWith(path + "^", StringComparison.OrdinalIgnoreCase))
                    continue;
                var below = Find(item.ProjectItems, treePath) ?? (item.SubProject != null ? Find(item.SubProject.ProjectItems, treePath) : null);
                if (below != null)
                    return below;
            }
            return null;
        }

        /// <summary>All projects including nested ones (PLC projects are nested in the TwinCAT project).</summary>
        public static IEnumerable<Project> AllProjects(DTE2 dte)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            var result = new List<Project>();
            if (dte.Solution == null)
                return result;
            foreach (Project project in dte.Solution.Projects)
                Collect(project, result, 0);
            return result;
        }

        private static void Collect(Project project, List<Project> result, int depth)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            if (project == null || depth > 6)
                return;
            result.Add(project);
            ProjectItems items = null;
            try
            {
                items = project.ProjectItems;
            }
            catch
            {
                // unloaded
            }
            if (items == null)
                return;
            foreach (ProjectItem item in items)
            {
                Project sub = null;
                try
                {
                    sub = item.SubProject;
                }
                catch
                {
                    // ignore
                }
                if (sub != null)
                    Collect(sub, result, depth + 1);
            }
        }

        /// <summary>Directory of the .plcproj that contains the tree item (used to find .tcformat.json).</summary>
        public static string ProjectDirectory(DTE2 dte, string treePath)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            // "TIPC^<Plc>^<Plc> Project^..." -> nested project name is the third segment
            var parts = (treePath ?? string.Empty).Split('^');
            string nested = parts.Length >= 3 ? parts[2] : null;
            string plc = parts.Length >= 2 ? parts[1] : null;

            foreach (Project project in AllProjects(dte))
            {
                string name, file;
                try
                {
                    name = project.Name;
                    file = project.FullName;
                }
                catch
                {
                    continue;
                }
                if (string.IsNullOrEmpty(file) || !file.EndsWith(".plcproj", StringComparison.OrdinalIgnoreCase))
                    continue;
                if (string.Equals(name, nested, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(name, plc, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(System.IO.Path.GetFileNameWithoutExtension(file), plc, StringComparison.OrdinalIgnoreCase))
                    return System.IO.Path.GetDirectoryName(file);
            }

            try
            {
                string solution = dte.Solution?.FullName;
                return string.IsNullOrEmpty(solution) ? null : System.IO.Path.GetDirectoryName(solution);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Directory on disk that corresponds to the object's PLC folder (PLC folders map to file system folders),
        /// so that a .tcformat.json next to the objects is found exactly like the CLI finds it.
        /// </summary>
        public static string ObjectDirectory(DTE2 dte, string treePath)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            string projectDirectory = ProjectDirectory(dte, treePath);
            if (string.IsNullOrEmpty(projectDirectory))
                return projectDirectory;

            // "TIPC^Plc^Plc Project^POUs^Motion^FB_Motor^M_Start" -> POUs\Motion\FB_Motor (non-existing tail is dropped)
            var parts = (treePath ?? string.Empty).Split('^');
            string directory = projectDirectory;
            for (int i = 3; i < parts.Length; i++)
            {
                string candidate = System.IO.Path.Combine(directory, parts[i]);
                if (!System.IO.Directory.Exists(candidate))
                    break;
                directory = candidate;
            }
            return directory;
        }

        public static string KindForNaming(int itemType)
        {
            switch (itemType)
            {
                case Gvl:
                case GvlParamList:
                    return "GVL";
                case Action:
                    return "ACTION";
                default:
                    return null;
            }
        }

        /// <summary>Object types with a declaration area (see InfoSys "Reading and setting the declaration and implementation of a POU").</summary>
        private static readonly HashSet<int> WithDeclaration = new HashSet<int>
        {
            602, 603, 604, 605, 606, 607, 623, 609, 610, 611, 612, 613, 614, 615, 618, 629,
        };

        /// <summary>Object types with an implementation area.</summary>
        private static readonly HashSet<int> WithImplementation = new HashSet<int>
        {
            602, 603, 604, 608, 609, 613, 614, 616,
        };

        public static bool HasDeclarationPart(int itemType) => WithDeclaration.Contains(itemType);

        public static bool HasImplementationPart(int itemType) => WithImplementation.Contains(itemType);

        public static string Describe(IEnumerable<ITcSmTreeItem> items) =>
            string.Join(", ", items.Select(i =>
            {
                try
                {
                    return i.PathName;
                }
                catch
                {
                    return "?";
                }
            }));
    }
}
