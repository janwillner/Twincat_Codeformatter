using System;
using System.Collections.Generic;

namespace TcFormatter.Lexing
{
    /// <summary>Vocabulary of IEC 61131-3 Structured Text as used by TwinCAT 3 (ExST).</summary>
    public static class StLanguage
    {
        public static readonly HashSet<string> Keywords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            // control flow
            "IF", "THEN", "ELSIF", "ELSE", "END_IF",
            "CASE", "OF", "END_CASE",
            "FOR", "TO", "BY", "DO", "END_FOR",
            "WHILE", "END_WHILE",
            "REPEAT", "UNTIL", "END_REPEAT",
            "EXIT", "CONTINUE", "RETURN", "JMP",
            // operators written as words
            "AND", "OR", "XOR", "NOT", "MOD", "AND_THEN", "OR_ELSE",
            // variable sections
            "VAR", "VAR_INPUT", "VAR_OUTPUT", "VAR_IN_OUT", "VAR_GLOBAL", "VAR_TEMP", "VAR_STAT",
            "VAR_EXTERNAL", "VAR_INST", "VAR_CONFIG", "END_VAR",
            "CONSTANT", "RETAIN", "PERSISTENT", "AT",
            // POUs
            "PROGRAM", "END_PROGRAM", "FUNCTION", "END_FUNCTION", "FUNCTION_BLOCK", "END_FUNCTION_BLOCK",
            "METHOD", "END_METHOD", "PROPERTY", "END_PROPERTY", "INTERFACE", "END_INTERFACE",
            "ACTION", "END_ACTION",
            "EXTENDS", "IMPLEMENTS", "ABSTRACT", "FINAL", "PUBLIC", "PRIVATE", "PROTECTED", "INTERNAL",
            // types
            "TYPE", "END_TYPE", "STRUCT", "END_STRUCT", "UNION", "END_UNION",
            "ARRAY", "POINTER", "REFERENCE",
            // literals / special
            "TRUE", "FALSE", "THIS", "SUPER",
            "__NEW", "__DELETE", "__QUERYINTERFACE", "__QUERYPOINTER", "__ISVALIDREF",
            "__TRY", "__CATCH", "__FINALLY", "__ENDTRY",
        };

        public static readonly HashSet<string> TypeNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "BOOL", "BIT", "BYTE", "WORD", "DWORD", "LWORD",
            "SINT", "USINT", "INT", "UINT", "DINT", "UDINT", "LINT", "ULINT",
            "REAL", "LREAL", "STRING", "WSTRING",
            "TIME", "LTIME", "DATE", "LDATE", "TIME_OF_DAY", "TOD", "LTOD", "DATE_AND_TIME", "DT", "LDT",
            "ANY", "ANY_NUM", "ANY_INT", "ANY_REAL", "ANY_BIT", "ANY_STRING", "ANY_DATE", "ANY_ELEMENTARY", "ANY_MAGNITUDE", "ANY_DERIVED",
            "XINT", "UXINT", "XWORD", "PVOID", "__XINT", "__UXINT", "__XWORD", "__SYSTEM",
        };

        /// <summary>Prefixes of duration literals (no '-' allowed inside apart from a leading sign).</summary>
        public static readonly HashSet<string> DurationPrefixes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "T", "TIME", "LT", "LTIME",
        };

        /// <summary>Prefixes of date / time-of-day literals (contain '-' and ':').</summary>
        public static readonly HashSet<string> DatePrefixes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "D", "DATE", "LD", "LDATE", "TOD", "TIME_OF_DAY", "LTOD", "DT", "DATE_AND_TIME", "LDT",
        };

        /// <summary>Keywords that open a variable section and are closed by END_VAR.</summary>
        public static readonly HashSet<string> VarSectionKeywords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "VAR", "VAR_INPUT", "VAR_OUTPUT", "VAR_IN_OUT", "VAR_GLOBAL", "VAR_TEMP", "VAR_STAT",
            "VAR_EXTERNAL", "VAR_INST", "VAR_CONFIG",
        };

        /// <summary>Keyword operators that are binary (need surrounding spaces).</summary>
        public static readonly HashSet<string> WordOperators = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "AND", "OR", "XOR", "MOD", "AND_THEN", "OR_ELSE",
        };

        public static bool IsKeyword(string word) => Keywords.Contains(word);

        public static bool IsTypeName(string word) => TypeNames.Contains(word);
    }
}
