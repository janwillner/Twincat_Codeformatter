using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using TcFormatter.Lexing;

namespace TcFormatter.Naming
{
    /// <summary>Checks the identifiers of one declaration text (POU, method, property, DUT, GVL, interface).</summary>
    public static class NamingChecker
    {
        private static readonly Regex IgnoreComment = new Regex(@"tcfmt\s*:\s*ignore-naming", RegexOptions.IgnoreCase);

        private static readonly HashSet<string> AccessModifiers = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "PUBLIC", "PRIVATE", "PROTECTED", "INTERNAL", "ABSTRACT", "FINAL",
        };

        private static readonly HashSet<string> SystemMethods = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "FB_init", "FB_reinit", "FB_exit", "FB_postinit", "FB_preinit",
        };

        private static readonly Dictionary<string, string> KindNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["FUNCTION_BLOCK"] = "Function block",
            ["FUNCTION"] = "Function",
            ["PROGRAM"] = "Program",
            ["INTERFACE"] = "Interface",
            ["METHOD"] = "Method",
            ["PROPERTY"] = "Property",
            ["ACTION"] = "Action",
            ["STRUCT"] = "Structure",
            ["UNION"] = "Union",
            ["ENUM"] = "Enumeration",
            ["ALIAS"] = "Alias type",
            ["GVL"] = "Global variable list",
        };

        /// <param name="declaration">Declaration text of the object.</param>
        /// <param name="rules">Naming rules.</param>
        /// <param name="objectName">Name of the object in the project tree. Needed for GVLs and actions, whose name is not part of the text.</param>
        /// <param name="objectKind">Optional kind ("GVL", "ACTION", ...) when it cannot be derived from the text.</param>
        public static IReadOnlyList<NamingDiagnostic> Check(string declaration, NamingRules rules, string objectName = null, string objectKind = null)
        {
            var result = new List<NamingDiagnostic>();
            if (rules == null || !rules.Enabled)
                return result;
            new Walker(declaration ?? string.Empty, rules, result).Run(objectName, objectKind);
            return result;
        }

        /// <summary>Checks an object that has no declaration text (e.g. an action).</summary>
        public static IReadOnlyList<NamingDiagnostic> CheckObjectName(string objectName, string objectKind, NamingRules rules)
        {
            var result = new List<NamingDiagnostic>();
            if (rules == null || !rules.Enabled || string.IsNullOrEmpty(objectName))
                return result;
            new Walker(string.Empty, rules, result).CheckObject(objectKind, objectName, 1, 1);
            return result;
        }

        private sealed class Walker
        {
            private readonly string _text;
            private readonly NamingRules _rules;
            private readonly List<NamingDiagnostic> _out;
            private readonly List<Token> _tokens;
            private readonly List<int> _lineStarts = new List<int> { 0 };
            private readonly HashSet<int> _ignoredLines = new HashSet<int>();
            private readonly List<Regex> _ignore;
            private int _i;

            public Walker(string text, NamingRules rules, List<NamingDiagnostic> output)
            {
                _text = text;
                _rules = rules;
                _out = output;
                for (int i = 0; i < text.Length; i++)
                {
                    if (text[i] == '\n')
                        _lineStarts.Add(i + 1);
                }
                var all = Lexer.Tokenize(text);
                foreach (var t in all.Where(t => t.IsComment && IgnoreComment.IsMatch(t.Text)))
                    _ignoredLines.Add(LineOf(t.Position));
                _tokens = all.Where(t => !t.IsTrivia && !t.IsComment && t.Kind != TokenKind.Pragma).ToList();
                _ignore = (rules.IgnoreNames ?? new List<string>())
                    .Where(p => !string.IsNullOrWhiteSpace(p))
                    .Select(p => new Regex(p, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
                    .ToList();
            }

            private Token Current => _i < _tokens.Count ? _tokens[_i] : null;

            private int LineOf(int position)
            {
                int line = 0;
                while (line + 1 < _lineStarts.Count && _lineStarts[line + 1] <= position)
                    line++;
                return line + 1;
            }

            private int ColumnOf(int position) => position - _lineStarts[LineOf(position) - 1] + 1;

            public void Run(string objectName, string objectKind)
            {
                ReadHeader(objectName, objectKind);
                while (_i < _tokens.Count)
                {
                    var t = _tokens[_i];
                    if (t.Kind == TokenKind.Keyword && StLanguage.VarSectionKeywords.Contains(t.Text))
                        ReadVarSection();
                    else if (t.IsKeyword("STRUCT") || t.IsKeyword("UNION"))
                        ReadMembers("STRUCT", false, "END_STRUCT", "END_UNION");
                    else
                        _i++;
                }
            }

            // ---------------------------------------------------------- header

            private void ReadHeader(string objectName, string objectKind)
            {
                var first = Current;
                if (first == null)
                {
                    if (objectName != null && objectKind != null)
                        CheckObject(objectKind, objectName, 1, 1);
                    return;
                }

                string word = first.Kind == TokenKind.Keyword ? first.Upper : null;
                switch (word)
                {
                    case "FUNCTION_BLOCK":
                    case "FUNCTION":
                    case "PROGRAM":
                    case "INTERFACE":
                    case "METHOD":
                    case "PROPERTY":
                    case "ACTION":
                        _i++;
                        while (Current != null && Current.Kind == TokenKind.Keyword && AccessModifiers.Contains(Current.Text))
                            _i++;
                        if (Current != null && Current.Kind == TokenKind.Identifier)
                        {
                            var name = Current;
                            _i++;
                            CheckObject(word, name.Text, LineOf(name.Position), ColumnOf(name.Position));
                        }
                        return;
                    case "TYPE":
                        _i++;
                        ReadType();
                        return;
                }

                if (first.IsKeyword("VAR_GLOBAL") || string.Equals(objectKind, "GVL", StringComparison.OrdinalIgnoreCase))
                {
                    if (objectName != null)
                        CheckObject("GVL", objectName, 1, 1);
                    return;
                }
                if (objectName != null && objectKind != null)
                    CheckObject(objectKind, objectName, 1, 1);
            }

            private void ReadType()
            {
                if (Current == null || Current.Kind != TokenKind.Identifier)
                    return;
                var name = Current;
                _i++;
                while (Current != null && !Current.IsOperator(":"))
                    _i++;
                _i++; // ':'
                string kind;
                if (Current == null)
                    kind = "ALIAS";
                else if (Current.IsKeyword("STRUCT"))
                    kind = "STRUCT";
                else if (Current.IsKeyword("UNION"))
                    kind = "UNION";
                else if (Current.IsOperator("("))
                    kind = "ENUM";
                else
                    kind = "ALIAS";

                CheckObject(kind, name.Text, LineOf(name.Position), ColumnOf(name.Position));

                if (kind == "ENUM")
                    ReadEnumValues();
            }

            private void ReadEnumValues()
            {
                _i++; // '('
                bool expectName = true;
                int depth = 0;
                while (Current != null)
                {
                    var t = Current;
                    if (t.IsOperator("(")) depth++;
                    else if (t.IsOperator(")"))
                    {
                        if (depth == 0)
                        {
                            _i++;
                            return;
                        }
                        depth--;
                    }
                    else if (t.IsOperator(",") && depth == 0)
                        expectName = true;
                    else if (expectName && t.Kind == TokenKind.Identifier)
                    {
                        expectName = false;
                        CheckEnumValue(t);
                    }
                    else
                        expectName = false;
                    _i++;
                }
            }

            // ---------------------------------------------------------- variables

            private void ReadVarSection()
            {
                string section = Current.Upper;
                _i++;
                bool constant = false;
                while (Current != null && Current.Kind == TokenKind.Keyword &&
                       (Current.Is("CONSTANT") || Current.Is("RETAIN") || Current.Is("PERSISTENT")))
                {
                    constant |= Current.Is("CONSTANT");
                    _i++;
                }
                ReadMembers(section, constant, "END_VAR");
            }

            private void ReadMembers(string section, bool constant, params string[] terminators)
            {
                if (Current != null && (Current.IsKeyword("STRUCT") || Current.IsKeyword("UNION")))
                    _i++;
                bool check = _rules.CheckedSections == null ||
                             _rules.CheckedSections.Any(s => string.Equals(s, section, StringComparison.OrdinalIgnoreCase));

                while (Current != null)
                {
                    if (Current.Kind == TokenKind.Keyword && terminators.Any(Current.Is))
                    {
                        _i++;
                        return;
                    }
                    if (Current.Kind == TokenKind.Keyword && StLanguage.VarSectionKeywords.Contains(Current.Text))
                        return; // malformed: missing END_VAR

                    // names
                    var names = new List<Token>();
                    while (Current != null && !Current.IsOperator(":") && !Current.IsOperator(";") &&
                           !(Current.Kind == TokenKind.Keyword && terminators.Any(Current.Is)))
                    {
                        if (Current.Kind == TokenKind.Identifier)
                            names.Add(Current);
                        else if (Current.IsKeyword("AT"))
                        {
                            _i++; // skip address
                        }
                        _i++;
                    }
                    if (Current == null || !Current.IsOperator(":"))
                    {
                        if (Current != null && Current.IsOperator(";"))
                            _i++;
                        continue;
                    }
                    _i++; // ':'

                    // type until ':=' or ';' (depth 0)
                    var type = new List<Token>();
                    int depth = 0;
                    while (Current != null)
                    {
                        if (depth == 0 && (Current.IsOperator(":=") || Current.IsOperator(";")))
                            break;
                        if (Current.IsOperator("(") || Current.IsOperator("[")) depth++;
                        if (Current.IsOperator(")") || Current.IsOperator("]")) depth--;
                        type.Add(Current);
                        _i++;
                    }
                    // skip initializer
                    depth = 0;
                    while (Current != null && !(depth == 0 && Current.IsOperator(";")))
                    {
                        if (Current.IsOperator("(") || Current.IsOperator("[")) depth++;
                        if (Current.IsOperator(")") || Current.IsOperator("]")) depth--;
                        if (Current.Kind == TokenKind.Keyword && terminators.Any(Current.Is))
                            break;
                        _i++;
                    }
                    if (Current != null && Current.IsOperator(";"))
                        _i++;

                    if (check)
                    {
                        foreach (var name in names)
                            CheckVariable(name, type, section, constant);
                    }
                }
            }

            private void CheckVariable(Token name, List<Token> type, string section, bool constant)
            {
                if (IsIgnored(name) || CheckIdentifier(name))
                    return;
                var severity = _rules.SeverityOf(NamingRuleIds.VariablePrefix);
                if (severity == NamingSeverity.Off)
                    return;

                var typePrefixes = ResolvePrefixes(type, 0, type.Count);
                if (typePrefixes == null)
                    return;

                string scope = string.Empty;
                if (_rules.ScopePrefixes != null && _rules.ScopePrefixes.TryGetValue(section, out var sp) && sp != null)
                    scope = sp;

                var accepted = typePrefixes.Select(p => scope + p).ToList();
                if (constant && !string.IsNullOrEmpty(_rules.ConstantPrefix))
                    accepted.Add(scope + _rules.ConstantPrefix);

                if (accepted.Any(p => HasPrefix(name.Text, p)))
                    return;

                string suggestion = Suggest(name.Text, accepted[0]);
                string typeText = string.Join(" ", type.Select(t => t.Text));
                string expected = string.Join("' or '", accepted.Distinct());
                Report(NamingRuleIds.VariablePrefix, severity, name,
                    $"Variable '{name.Text}' of type {typeText} should start with '{expected}'" +
                    (_rules.RequirePascalCaseAfterPrefix ? " followed by an upper case letter" : "") +
                    $" (e.g. '{suggestion}').", suggestion);
            }

            private bool HasPrefix(string name, string prefix)
            {
                if (!name.StartsWith(prefix, StringComparison.Ordinal))
                    return false;
                if (!_rules.RequirePascalCaseAfterPrefix)
                    return name.Length > prefix.Length || prefix.Length == 0;
                if (name.Length <= prefix.Length)
                    return false;
                char c = name[prefix.Length];
                return char.IsUpper(c) || char.IsDigit(c);
            }

            private static string Suggest(string name, string prefix)
            {
                // a lower case run followed by an upper case letter is an existing (wrong) prefix: replace it
                int lower = 0;
                while (lower < name.Length && char.IsLower(name[lower]))
                    lower++;
                string body;
                if (lower > 0 && lower < name.Length && char.IsUpper(name[lower]))
                    body = name.Substring(lower);
                else if (prefix.Length > 0 && name.StartsWith(prefix, StringComparison.Ordinal) && name.Length > prefix.Length)
                    body = name.Substring(prefix.Length);
                else
                    body = name;
                body = body.TrimStart('_');
                if (body.Length == 0)
                    return prefix + name;
                return prefix + char.ToUpperInvariant(body[0]) + body.Substring(1);
            }

            /// <summary>Accepted type prefixes for the type expression, or null when the type is unknown.</summary>
            private List<string> ResolvePrefixes(List<Token> type, int start, int end)
            {
                if (start >= end)
                    return null;
                var first = type[start];

                if (first.IsKeyword("ARRAY"))
                {
                    int of = -1, depth = 0;
                    for (int i = start; i < end; i++)
                    {
                        if (type[i].IsOperator("[")) depth++;
                        else if (type[i].IsOperator("]")) depth--;
                        else if (depth == 0 && type[i].IsKeyword("OF"))
                        {
                            of = i;
                            break;
                        }
                    }
                    return Combine(_rules.ArrayPrefix, _rules.ArrayIncludesElementPrefix, of < 0 ? null : ResolvePrefixes(type, of + 1, end));
                }
                if ((first.IsKeyword("POINTER") || first.IsKeyword("REFERENCE")) && start + 1 < end && type[start + 1].IsKeyword("TO"))
                {
                    bool pointer = first.IsKeyword("POINTER");
                    var target = ResolvePrefixes(type, start + 2, end);
                    return pointer
                        ? Combine(_rules.PointerPrefix, _rules.PointerIncludesTargetPrefix, target)
                        : Combine(_rules.ReferencePrefix, _rules.ReferenceIncludesTargetPrefix, target);
                }

                // base type name; qualified names (Tc2_Standard.TON) use the last part
                string typeName = first.Text;
                for (int i = start + 1; i + 1 < end && type[i].IsOperator("."); i += 2)
                    typeName = type[i + 1].Text;

                return LookupTypePrefix(typeName);
            }

            private static List<string> Combine(string prefix, bool includeInner, List<string> inner)
            {
                prefix = prefix ?? string.Empty;
                if (!includeInner || inner == null)
                    return new List<string> { prefix };
                return inner.Select(p => prefix + p).ToList();
            }

            private List<string> LookupTypePrefix(string typeName)
            {
                var map = _rules.TypePrefixes;
                if (map == null)
                    return null;
                string value = null;
                foreach (var pair in map)
                {
                    if (pair.Key.IndexOf('*') < 0 && string.Equals(pair.Key, typeName, StringComparison.OrdinalIgnoreCase))
                    {
                        value = pair.Value;
                        break;
                    }
                }
                if (value == null)
                {
                    foreach (var pair in map)
                    {
                        if (pair.Key.IndexOf('*') >= 0 && Glob(pair.Key, typeName))
                        {
                            value = pair.Value;
                            break;
                        }
                    }
                }
                if (value == null)
                    return null;
                return value.Split(',').Select(v => v.Trim()).ToList();
            }

            private static bool Glob(string pattern, string text) =>
                Regex.IsMatch(text, "^" + Regex.Escape(pattern).Replace("\\*", ".*") + "$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

            // ---------------------------------------------------------- objects, members, enum values

            public void CheckObject(string kind, string name, int line, int column)
            {
                if (IsIgnoredName(name) || _ignoredLines.Contains(line))
                    return;
                if (CheckIdentifierText(name, line, column))
                    return;

                kind = kind.ToUpperInvariant();
                string prefix = null;
                _rules.ObjectPrefixes?.TryGetValue(kind, out prefix);
                bool member = kind == "METHOD" || kind == "PROPERTY" || kind == "ACTION";
                string kindName = KindNames.TryGetValue(kind, out var k) ? k : kind;

                if (member && string.IsNullOrEmpty(prefix))
                {
                    if (SystemMethods.Contains(name))
                        return;
                    var severity = _rules.SeverityOf(NamingRuleIds.MemberName);
                    if (severity == NamingSeverity.Off)
                        return;
                    bool upper = char.IsUpper(name[0]);
                    bool underscore = !_rules.AllowUnderscoreInMemberNames && name.IndexOf('_') >= 0;
                    if (upper && !underscore)
                        return;
                    string suggestion = MemberSuggestion(name);
                    Report(NamingRuleIds.MemberName, severity, name, line, column,
                        $"{kindName} '{name}' should be PascalCase without prefix or underscores (e.g. '{suggestion}').", suggestion);
                    return;
                }

                if (string.IsNullOrEmpty(prefix))
                    return;
                var sev = _rules.SeverityOf(NamingRuleIds.ObjectPrefix);
                if (sev == NamingSeverity.Off)
                    return;
                bool ok = name.StartsWith(prefix, StringComparison.Ordinal) && name.Length > prefix.Length &&
                          (!_rules.RequirePascalCaseAfterPrefix || char.IsUpper(name[prefix.Length]) || char.IsDigit(name[prefix.Length]));
                if (ok)
                    return;
                string body = StripObjectPrefix(name);
                string suggest = prefix + (body.Length > 0 ? char.ToUpperInvariant(body[0]) + body.Substring(1) : body);
                Report(NamingRuleIds.ObjectPrefix, sev, name, line, column,
                    $"{kindName} '{name}' should start with '{prefix}'" +
                    (_rules.RequirePascalCaseAfterPrefix ? " followed by an upper case letter" : "") +
                    $" (e.g. '{suggest}').", suggest);
            }

            private static string StripObjectPrefix(string name)
            {
                // "Prg_Main" -> "Main", "fb_motor" -> "motor", "Motor" -> "Motor"
                int underscore = name.IndexOf('_');
                if (underscore > 0 && underscore <= 4 && underscore < name.Length - 1)
                    return name.Substring(underscore + 1);
                return name;
            }

            private static string MemberSuggestion(string name)
            {
                var parts = name.Split(new[] { '_' }, StringSplitOptions.RemoveEmptyEntries).ToList();
                // drop a short lower/upper prefix like "M_" or "P_"
                if (parts.Count > 1 && parts[0].Length <= 2)
                    parts.RemoveAt(0);
                return string.Concat(parts.Select(p => char.ToUpperInvariant(p[0]) + p.Substring(1)));
            }

            private void CheckEnumValue(Token value)
            {
                if (IsIgnored(value) || CheckIdentifier(value) || !_rules.EnumValuesPascalCase)
                    return;
                var severity = _rules.SeverityOf(NamingRuleIds.EnumValue);
                if (severity == NamingSeverity.Off || char.IsUpper(value.Text[0]))
                    return;
                string suggestion = char.ToUpperInvariant(value.Text[0]) + value.Text.Substring(1);
                Report(NamingRuleIds.EnumValue, severity, value,
                    $"Enum value '{value.Text}' should start with an upper case letter (e.g. '{suggestion}').", suggestion);
            }

            /// <summary>Reports invalid identifiers; returns true when one was reported.</summary>
            private bool CheckIdentifier(Token name) =>
                CheckIdentifierText(name.Text, LineOf(name.Position), ColumnOf(name.Position));

            private bool CheckIdentifierText(string name, int line, int column)
            {
                string problem = null;
                if (name.Contains("__"))
                    problem = "must not contain double underscores";
                else if (name.StartsWith("_", StringComparison.Ordinal))
                    problem = "must not start with an underscore";
                else if (name.Any(c => c > 127))
                    problem = "should only contain the characters A-Z, a-z, 0-9 and _";
                if (problem == null)
                    return false;
                var severity = _rules.SeverityOf(NamingRuleIds.InvalidIdentifier);
                if (severity != NamingSeverity.Off)
                    Report(NamingRuleIds.InvalidIdentifier, severity, name, line, column, $"Identifier '{name}' {problem}.", null);
                return true;
            }

            private bool IsIgnored(Token name) => _ignoredLines.Contains(LineOf(name.Position)) || IsIgnoredName(name.Text);

            private bool IsIgnoredName(string name) => _ignore.Any(r => r.IsMatch(name));

            private void Report(string ruleId, NamingSeverity severity, Token name, string message, string suggestion) =>
                Report(ruleId, severity, name.Text, LineOf(name.Position), ColumnOf(name.Position), message, suggestion);

            private void Report(string ruleId, NamingSeverity severity, string name, int line, int column, string message, string suggestion) =>
                _out.Add(new NamingDiagnostic(ruleId, severity, name, message, line, column, suggestion));
        }
    }
}
