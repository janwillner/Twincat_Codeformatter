using System;
using System.Collections.Generic;

namespace TcFormatter.Naming
{
    public enum NamingSeverity { Off, Info, Warning, Error }

    public static class NamingRuleIds
    {
        /// <summary>POU / DUT / GVL / interface name prefix (FB_, F_, ST_, ...).</summary>
        public const string ObjectPrefix = "TCN001";

        /// <summary>Variable prefix derived from data type and scope (b, n, f, ...).</summary>
        public const string VariablePrefix = "TCN002";

        /// <summary>Method and property names (PascalCase without prefix by default).</summary>
        public const string MemberName = "TCN003";

        /// <summary>Enum values (PascalCase by default).</summary>
        public const string EnumValue = "TCN004";

        /// <summary>Invalid characters, double underscores, leading underscores.</summary>
        public const string InvalidIdentifier = "TCN005";
    }

    /// <summary>
    /// Naming conventions. Defaults follow the Beckhoff TwinCAT 3 programming conventions (Hungarian notation).
    /// Maps 1:1 to the "naming" section of .tcformat.json.
    /// </summary>
    public sealed class NamingRules
    {
        public bool Enabled { get; set; } = true;

        /// <summary>Severity per rule id (see <see cref="NamingRuleIds"/>). Missing ids use <see cref="DefaultSeverity"/>.</summary>
        public Dictionary<string, NamingSeverity> Severities { get; set; } = new Dictionary<string, NamingSeverity>(StringComparer.OrdinalIgnoreCase);

        public NamingSeverity DefaultSeverity { get; set; } = NamingSeverity.Warning;

        /// <summary>
        /// Variable prefix per data type. Keys are type names; '*' is a wildcard (e.g. "FB_*").
        /// Values may list alternatives separated by ',' (e.g. "n,i"). Exact keys win over wildcard keys;
        /// wildcard keys are tried in order of insertion.
        /// </summary>
        public Dictionary<string, string> TypePrefixes { get; set; } = DefaultTypePrefixes();

        public string ArrayPrefix { get; set; } = "a";

        /// <summary>When true, arrays need the element prefix too: ARRAY OF BOOL → "ab".</summary>
        public bool ArrayIncludesElementPrefix { get; set; }

        public string PointerPrefix { get; set; } = "p";

        /// <summary>When true, pointers need the target prefix too: POINTER TO INT → "pn".</summary>
        public bool PointerIncludesTargetPrefix { get; set; }

        public string ReferencePrefix { get; set; } = "ref";

        public bool ReferenceIncludesTargetPrefix { get; set; }

        /// <summary>Additional accepted prefix for constants (VAR CONSTANT / VAR_GLOBAL CONSTANT). Empty = none.</summary>
        public string ConstantPrefix { get; set; } = "c";

        /// <summary>
        /// Prefix required before the type prefix per variable section, e.g. { "VAR_GLOBAL": "g" } → "gbStop".
        /// Keys: VAR, VAR_INPUT, VAR_OUTPUT, VAR_IN_OUT, VAR_GLOBAL, VAR_TEMP, VAR_STAT, VAR_INST, STRUCT.
        /// </summary>
        public Dictionary<string, string> ScopePrefixes { get; set; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>Object name prefix per object kind. Empty string = no prefix required.</summary>
        public Dictionary<string, string> ObjectPrefixes { get; set; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["FUNCTION_BLOCK"] = "FB_",
            ["FUNCTION"] = "F_",
            ["PROGRAM"] = "P_",
            ["INTERFACE"] = "I_",
            ["STRUCT"] = "ST_",
            ["ENUM"] = "E_",
            ["UNION"] = "U_",
            ["ALIAS"] = "T_",
            ["GVL"] = "GVL_",
            ["METHOD"] = "",
            ["PROPERTY"] = "",
            ["ACTION"] = "",
        };

        /// <summary>The first character after the prefix must be an upper case letter or digit (PascalCase).</summary>
        public bool RequirePascalCaseAfterPrefix { get; set; } = true;

        /// <summary>Methods, properties and actions: allow '_' inside the name.</summary>
        public bool AllowUnderscoreInMemberNames { get; set; }

        /// <summary>Enum values must start with an upper case letter.</summary>
        public bool EnumValuesPascalCase { get; set; } = true;

        /// <summary>Regular expressions of names that are never reported (variables and objects).</summary>
        public List<string> IgnoreNames { get; set; } = new List<string> { "^MAIN$" };

        /// <summary>Variable sections that are checked.</summary>
        public List<string> CheckedSections { get; set; } = new List<string>
        {
            "VAR", "VAR_INPUT", "VAR_OUTPUT", "VAR_IN_OUT", "VAR_GLOBAL", "VAR_TEMP", "VAR_STAT", "VAR_INST", "STRUCT",
        };

        public NamingSeverity SeverityOf(string ruleId) =>
            Severities != null && Severities.TryGetValue(ruleId, out var s) ? s : DefaultSeverity;

        public static Dictionary<string, string> DefaultTypePrefixes() => new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["BOOL"] = "b",
            ["BIT"] = "b",
            ["BYTE"] = "n,i",
            ["WORD"] = "n,i",
            ["DWORD"] = "n,i",
            ["LWORD"] = "n,i",
            ["SINT"] = "n,i",
            ["USINT"] = "n,i",
            ["INT"] = "n,i",
            ["UINT"] = "n,i",
            ["DINT"] = "n,i",
            ["UDINT"] = "n,i",
            ["LINT"] = "n,i",
            ["ULINT"] = "n,i",
            ["XINT"] = "n,i",
            ["UXINT"] = "n,i",
            ["XWORD"] = "n,i",
            ["__XINT"] = "n,i",
            ["__UXINT"] = "n,i",
            ["__XWORD"] = "n,i",
            ["PVOID"] = "p",
            ["REAL"] = "f",
            ["LREAL"] = "f",
            ["STRING"] = "s",
            ["WSTRING"] = "ws,s",
            ["TIME"] = "t",
            ["LTIME"] = "t",
            ["DATE"] = "d",
            ["LDATE"] = "d",
            ["DATE_AND_TIME"] = "dt",
            ["DT"] = "dt",
            ["LDT"] = "dt",
            ["TIME_OF_DAY"] = "tod",
            ["TOD"] = "tod",
            ["LTOD"] = "tod",
            // standard function blocks
            ["TON"] = "fb",
            ["TOF"] = "fb",
            ["TP"] = "fb",
            ["LTON"] = "fb",
            ["LTOF"] = "fb",
            ["LTP"] = "fb",
            ["R_TRIG"] = "fb",
            ["F_TRIG"] = "fb",
            ["CTU"] = "fb",
            ["CTD"] = "fb",
            ["CTUD"] = "fb",
            ["RS"] = "fb",
            ["SR"] = "fb",
            // project types by naming convention
            ["FB_*"] = "fb",
            ["MC_*"] = "fb",
            ["ST_*"] = "st",
            ["E_*"] = "e",
            ["I_*"] = "i",
        };
    }
}
