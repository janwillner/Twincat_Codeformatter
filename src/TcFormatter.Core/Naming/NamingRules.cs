using System;
using System.Collections.Generic;
using System.ComponentModel;

namespace TcFormatter.Naming
{
    public enum NamingSeverity { Off, Info, Warning, Error }

    /// <summary>Required casing of a name (after its prefix).</summary>
    public enum NameCase
    {
        /// <summary>Not checked.</summary>
        Any,

        /// <summary>Starts with an upper case letter or digit: "MaxCycles".</summary>
        PascalCase,

        /// <summary>Upper case letters, digits and '_': "MAX_CYCLES".</summary>
        UpperCase,
    }

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
        [Category("1 General"), Description("Enable the naming check.")]
        public bool Enabled { get; set; } = true;

        /// <summary>Severity per rule id (see <see cref="NamingRuleIds"/>). Missing ids use <see cref="DefaultSeverity"/>.</summary>
        [Category("1 General"), Description("Severity per rule: TCN001 object prefix, TCN002 variable prefix, TCN003 method/property name, TCN004 enum value, TCN005 invalid identifier. Values: Off, Info, Warning, Error.")]
        public Dictionary<string, NamingSeverity> Severities { get; set; } = new Dictionary<string, NamingSeverity>(StringComparer.OrdinalIgnoreCase);

        [Category("1 General"), Description("Severity of rules without an entry in 'severities'.")]
        public NamingSeverity DefaultSeverity { get; set; } = NamingSeverity.Warning;

        /// <summary>
        /// Variable prefix per data type. Keys are type names; '*' is a wildcard (e.g. "FB_*").
        /// Values may list alternatives separated by ',' (e.g. "n,i"). Exact keys win over wildcard keys;
        /// wildcard keys are tried in order of insertion.
        /// </summary>
        [Category("2 Variables"), Description("Variable prefix per data type. '*' is a wildcard (e.g. FB_*), alternatives are separated by ',' (e.g. n,i). Unknown types are not checked.")]
        public Dictionary<string, string> TypePrefixes { get; set; } = DefaultTypePrefixes();

        [Category("2 Variables"), Description("Prefix of ARRAY variables.")]
        public string ArrayPrefix { get; set; } = "a";

        /// <summary>When true, arrays need the element prefix too: ARRAY OF BOOL → "ab".</summary>
        [Category("2 Variables"), Description("Arrays also need the element prefix (ARRAY OF BOOL -> ab).")]
        public bool ArrayIncludesElementPrefix { get; set; }

        [Category("2 Variables"), Description("Prefix of POINTER variables.")]
        public string PointerPrefix { get; set; } = "p";

        /// <summary>When true, pointers need the target prefix too: POINTER TO INT → "pn".</summary>
        [Category("2 Variables"), Description("Pointers also need the target prefix (POINTER TO INT -> pn).")]
        public bool PointerIncludesTargetPrefix { get; set; }

        [Category("2 Variables"), Description("Prefix of REFERENCE variables.")]
        public string ReferencePrefix { get; set; } = "ref";

        [Category("2 Variables"), Description("References also need the target prefix.")]
        public bool ReferenceIncludesTargetPrefix { get; set; }

        /// <summary>Additional accepted prefix for constants (VAR CONSTANT / VAR_GLOBAL CONSTANT). Empty = none.</summary>
        [Category("2 Variables"), Description("Additional accepted prefix for constants (VAR CONSTANT). Empty = none.")]
        public string ConstantPrefix { get; set; } = "c";

        [Category("2 Variables"), Description("Casing of constant names after the prefix (Any, PascalCase, UpperCase). UpperCase: 'dn_MAX_LIGHT_CYCLES'.")]
        public NameCase ConstantNameCase { get; set; } = NameCase.Any;

        [Category("2 Variables"), Description("Text required after the type prefix of local variables (VAR / VAR_TEMP) of methods, e.g. 'Temp' -> 'lr_TempAngle'. Empty = none.")]
        public string MethodLocalMarker { get; set; } = "";

        /// <summary>
        /// Prefix required before the type prefix per variable section, e.g. { "VAR_GLOBAL": "g" } → "gbStop".
        /// Keys: VAR, VAR_INPUT, VAR_OUTPUT, VAR_IN_OUT, VAR_GLOBAL, VAR_TEMP, VAR_STAT, VAR_INST, STRUCT.
        /// </summary>
        [Category("2 Variables"), Description("Prefix required before the type prefix per section, e.g. VAR_GLOBAL: g -> gbStop. Keys: VAR, VAR_INPUT, VAR_OUTPUT, VAR_IN_OUT, VAR_GLOBAL, VAR_TEMP, VAR_STAT, VAR_INST, STRUCT.")]
        public Dictionary<string, string> ScopePrefixes { get; set; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>Object name prefix per object kind. Empty string = no prefix required.</summary>
        [Category("3 Objects"), Description("Name prefix per object kind (FUNCTION_BLOCK, FUNCTION, PROGRAM, INTERFACE, STRUCT, ENUM, UNION, ALIAS, GVL, METHOD, PROPERTY, ACTION). Empty = PascalCase without prefix.")]
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
        [Category("3 Objects"), Description("The character after a prefix must be an upper case letter or digit.")]
        public bool RequirePascalCaseAfterPrefix { get; set; } = true;

        /// <summary>Methods, properties and actions: allow '_' inside the name.</summary>
        [Category("3 Objects"), Description("Allow '_' in method, property and action names.")]
        public bool AllowUnderscoreInMemberNames { get; set; }

        /// <summary>Enum values must start with an upper case letter.</summary>
        [Category("3 Objects"), Description("Casing of enum values (Any, PascalCase, UpperCase).")]
        public NameCase EnumValueCase { get; set; } = NameCase.PascalCase;

        [Category("3 Objects"), Description("Allow names that start with '_' (e.g. object prefixes like _FB_ / _FC_).")]
        public bool AllowLeadingUnderscore { get; set; }

        /// <summary>Regular expressions of names that are never reported (variables and objects).</summary>
        [Category("1 General"), Description("Regular expressions of names that are never reported.")]
        public List<string> IgnoreNames { get; set; } = new List<string> { "^MAIN$" };

        /// <summary>Variable sections that are checked.</summary>
        [Category("1 General"), Description("Variable sections that are checked.")]
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
            ["I_*"] = "i,ip",
        };
    }
}
