using System;
using System.Collections.Generic;
using System.Reflection;
using EnvDTE;
using EnvDTE80;
using Microsoft.VisualStudio.Shell;

namespace TcFormatter.Vsix.Ui
{
    /// <summary>
    /// The context menu of the Error List in TcXaeShell has a "Clear" entry (a TwinCAT command) that only removes
    /// TwinCAT's own messages. This hook watches the executed commands: a command whose name contains "Clear",
    /// executed while the Error List is the active window, removes the naming entries as well.
    /// </summary>
    /// <remarks>
    /// The command ids of menu entries cannot be read in VS 2022 (every control reports id 0), so the command is
    /// recognised by name and context instead of a hard coded id. Events are attached late bound: the EnvDTE
    /// signatures differ between VS 2017/2019 and VS 2022 (MissingMethodException when bound at compile time).
    /// </remarks>
    internal sealed class ErrorListClearHook
    {
        private const string ErrorListWindowKind = "{D78612C7-9962-4B83-95D9-268046DAD23A}"; // vsWindowKindErrorList

        private readonly DTE2 _dte;
        private readonly NamingErrorList _errors;
        private readonly Dictionary<string, string> _names = new Dictionary<string, string>();
        private readonly LinkedList<string> _recent = new LinkedList<string>();

        // the event object must stay referenced, otherwise the COM events are released
        private object _commandEvents;

        private ErrorListClearHook(DTE2 dte, NamingErrorList errors)
        {
            _dte = dte;
            _errors = errors;
        }

        /// <summary>What was hooked, for the log and the diagnostics.</summary>
        public string Description { get; private set; }

        /// <summary>The last commands executed while the Error List was active (diagnostics).</summary>
        public IEnumerable<string> RecentErrorListCommands => _recent;

        public static ErrorListClearHook Attach(DTE2 dte, NamingErrorList errors)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            var hook = new ErrorListClearHook(dte, errors);
            try
            {
                // Events.CommandEvents[guid, id] with an empty guid = all commands
                object events = dte.Events.GetType().InvokeMember(
                    "CommandEvents", BindingFlags.GetProperty, null, dte.Events, new object[] { "{00000000-0000-0000-0000-000000000000}", 0 });
                var after = typeof(_dispCommandEvents_Event).GetEvent("AfterExecute");
                var handler = Delegate.CreateDelegate(after.EventHandlerType, hook,
                    typeof(ErrorListClearHook).GetMethod(nameof(OnAfterExecute), BindingFlags.Instance | BindingFlags.NonPublic));
                after.AddEventHandler(events, handler);
                hook._commandEvents = events;
                hook.Description = "watching commands executed in the Error List";
            }
            catch (Exception ex)
            {
                hook.Description = "not attached: " + ex.Message;
            }
            return hook;
        }

        private void OnAfterExecute(string guid, int id, object customIn, object customOut)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            try
            {
                if (!string.Equals(_dte.ActiveWindow?.ObjectKind, ErrorListWindowKind, StringComparison.OrdinalIgnoreCase))
                    return;
                string name = NameOf(guid, id);
                Remember(name);
                if (name.IndexOf("clear", StringComparison.OrdinalIgnoreCase) >= 0)
                    _errors.Clear();
            }
            catch (Exception)
            {
                // never disturb the command that was executed
            }
        }

        private string NameOf(string guid, int id)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            string key = guid + ":" + id;
            if (_names.TryGetValue(key, out var name))
                return name;
            try
            {
                var command = _dte.Commands.Item(guid, id);
                name = $"{command.Name} / {command.LocalizedName}";
            }
            catch (Exception)
            {
                name = string.Empty;
            }
            _names[key] = name;
            return name;
        }

        private void Remember(string name)
        {
            _recent.AddFirst(name);
            while (_recent.Count > 10)
                _recent.RemoveLast();
        }

        /// <summary>Commands whose name contains <paramref name="filter"/> (diagnostics).</summary>
        public static List<string> FindCommands(DTE2 dte, string filter)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            var result = new List<string>();
            foreach (Command command in dte.Commands)
            {
                try
                {
                    string name = command.Name ?? string.Empty;
                    string local = command.LocalizedName ?? string.Empty;
                    if (name.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0 || local.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
                        result.Add($"{command.Guid} {command.ID} '{name}' '{local}'");
                }
                catch (Exception)
                {
                    // some commands cannot be inspected
                }
            }
            return result;
        }

        /// <summary>Activates the Error List and executes <paramref name="commandName"/> there, like a click (diagnostics).</summary>
        public static void RunInErrorList(DTE2 dte, string commandName)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            dte.ToolWindows.ErrorList.Parent.Activate();
            dte.ExecuteCommand(commandName);
        }
    }
}
