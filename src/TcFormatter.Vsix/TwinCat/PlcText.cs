using System;
using System.Reflection;
using System.Runtime.InteropServices;
using TCatSysManagerLib;

namespace TcFormatter.Vsix.TwinCat
{
    /// <summary>
    /// Reads and writes DeclarationText / ImplementationText of a PLC tree item.
    /// First tries the early bound interface (ITcPlcDeclaration / ITcPlcImplementation); when the object
    /// does not support it in-process (some XAE versions throw an SEHException) it falls back to late
    /// binding through IDispatch, which is what PowerShell scripts of the Automation Interface use.
    /// </summary>
    internal static class PlcText
    {
        public const string Declaration = "DeclarationText";
        public const string Implementation = "ImplementationText";

        /// <summary>Last access path that worked ("interface" or "IDispatch"), for diagnostics.</summary>
        public static string LastAccessPath { get; private set; } = "none";

        public static bool HasDeclaration(ITcSmTreeItem item) => item is ITcPlcDeclaration || HasDispatchProperty(item, Declaration);

        public static bool HasImplementation(ITcSmTreeItem item) => item is ITcPlcImplementation || HasDispatchProperty(item, Implementation);

        public static string Read(ITcSmTreeItem item, string property)
        {
            try
            {
                string text = property == Declaration
                    ? (item as ITcPlcDeclaration)?.DeclarationText
                    : (item as ITcPlcImplementation)?.ImplementationText;
                if (text != null)
                {
                    LastAccessPath = "interface";
                    return text;
                }
            }
            catch (ExternalException)
            {
                // fall back to IDispatch below
            }

            object value = item.GetType().InvokeMember(property, BindingFlags.GetProperty, null, item, null);
            LastAccessPath = "IDispatch";
            return value as string ?? string.Empty;
        }

        public static void Write(ITcSmTreeItem item, string property, string text)
        {
            try
            {
                if (property == Declaration && item is ITcPlcDeclaration declaration)
                {
                    declaration.DeclarationText = text;
                    return;
                }
                if (property == Implementation && item is ITcPlcImplementation implementation)
                {
                    implementation.ImplementationText = text;
                    return;
                }
            }
            catch (SEHException)
            {
                // fall back to IDispatch below
            }
            catch (InvalidCastException)
            {
                // fall back to IDispatch below
            }

            item.GetType().InvokeMember(property, BindingFlags.SetProperty, null, item, new object[] { text });
        }

        /// <summary>True when the implementation is Structured Text (unknown languages are treated as not ST).</summary>
        public static bool IsStructuredText(ITcSmTreeItem item)
        {
            try
            {
                if (item is ITcPlcImplementation implementation)
                    return implementation.Language == IECLANGUAGETYPES.IECLANGUAGE_ST;
            }
            catch (ExternalException)
            {
                // fall back to IDispatch below
            }
            try
            {
                object language = item.GetType().InvokeMember("Language", BindingFlags.GetProperty, null, item, null);
                return Convert.ToInt32(language) == (int)IECLANGUAGETYPES.IECLANGUAGE_ST;
            }
            catch
            {
                return false;
            }
        }

        private static bool HasDispatchProperty(ITcSmTreeItem item, string property)
        {
            try
            {
                item.GetType().InvokeMember(property, BindingFlags.GetProperty, null, item, null);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
