using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Antlr4.Runtime;
using Antlr4.Runtime.Tree;
using TcFormatter.Formatting;
using TcFormatter.Syntax;
using TcFormatter.Syntax.Generated;

namespace TcFormatter.Naming
{
    /// <summary>
    /// Checks the identifiers of one declaration text (POU, method, property, DUT, GVL, interface). The declaration is
    /// parsed; a declaration with a syntax error is refused with one <see cref="NamingRuleIds.SyntaxError"/> error.
    /// </summary>
    public static class NamingChecker
    {
        private static readonly Regex IgnoreComment = new Regex(@"tcfmt\s*:\s*ignore-naming", RegexOptions.IgnoreCase);

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

            var parse = StSyntax.ParseTree(declaration ?? string.Empty, CodePart.Declaration);
            if (parse.Error != null)
            {
                result.Add(new NamingDiagnostic(NamingRuleIds.SyntaxError, NamingSeverity.Error, string.Empty,
                    "Syntax error, names not checked: " + parse.Error.Message, parse.Error.Line, parse.Error.Column, null));
                return result;
            }
            new Checker(rules, result, parse).Run((StParser.DeclarationPartContext)parse.Tree, objectName, objectKind);
            return result;
        }

        /// <summary>Checks an object that has no declaration text (e.g. an action).</summary>
        public static IReadOnlyList<NamingDiagnostic> CheckObjectName(string objectName, string objectKind, NamingRules rules)
        {
            var result = new List<NamingDiagnostic>();
            if (rules == null || !rules.Enabled || string.IsNullOrEmpty(objectName))
                return result;
            new Checker(rules, result, null).CheckObject(objectKind, objectName, 1, 1);
            return result;
        }

        /// <summary>A name in the declaration with its 1-based position.</summary>
        private readonly struct NameToken
        {
            public NameToken(IToken token)
            {
                Text = token.Text;
                Line = token.Line;
                Column = token.Column + 1;
            }

            public string Text { get; }
            public int Line { get; }
            public int Column { get; }
        }

        private sealed class Checker
        {
            private readonly NamingRules _rules;
            private readonly List<NamingDiagnostic> _out;
            private readonly IList<IToken> _tokens;
            private readonly HashSet<int> _ignoredLines = new HashSet<int>();
            private readonly List<Regex> _ignore;

            /// <summary>Header keyword of the checked declaration (FUNCTION_BLOCK, METHOD, ...), or null.</summary>
            private string _headerKind;

            public Checker(NamingRules rules, List<NamingDiagnostic> output, ParseResult parse)
            {
                _rules = rules;
                _out = output;
                _tokens = parse?.Tokens ?? new List<IToken>();
                foreach (var t in _tokens.Where(IsComment).Where(t => IgnoreComment.IsMatch(t.Text)))
                    _ignoredLines.Add(t.Line);
                _ignore = (rules.IgnoreNames ?? new List<string>())
                    .Select(TryRegex)
                    .Where(r => r != null)
                    .ToList();
            }

            private static bool IsComment(IToken t) =>
                t.Type == StLexer.LINE_COMMENT || t.Type == StLexer.BLOCK_COMMENT || t.Type == StLexer.C_COMMENT;

            /// <summary>Invalid patterns are skipped here; the configuration loader reports them as warnings.</summary>
            private static Regex TryRegex(string pattern)
            {
                if (string.IsNullOrWhiteSpace(pattern))
                    return null;
                try
                {
                    return new Regex(pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
                }
                catch (ArgumentException)
                {
                    return null;
                }
            }

            public void Run(StParser.DeclarationPartContext declaration, string objectName, string objectKind)
            {
                var items = declaration.declarationItem();
                CheckHeader(items.FirstOrDefault(i => i.SEMI() == null), objectName, objectKind);

                foreach (var item in items)
                {
                    if (item.varSection() != null)
                        CheckVarSection(item.varSection());
                    else if (item.typeDeclaration() != null)
                    {
                        foreach (var definition in item.typeDeclaration().typeDefinition())
                            CheckTypeDefinition(definition);
                    }
                }
            }

            // ---------------------------------------------------------- header

            /// <summary>The object itself: POU header, GVL or the kind passed by the caller.</summary>
            private void CheckHeader(StParser.DeclarationItemContext first, string objectName, string objectKind)
            {
                var header = first?.pouHeader();
                if (header != null)
                {
                    _headerKind = header.Start.Text.ToUpperInvariant();
                    var name = new NameToken(header.name().Start);
                    CheckObject(_headerKind, name.Text, name.Line, name.Column);
                    return;
                }
                if (first?.typeDeclaration() != null)
                    return; // checked with each type definition

                bool gvl = first?.varSection()?.varSectionKeyword().VAR_GLOBAL() != null ||
                           string.Equals(objectKind, "GVL", StringComparison.OrdinalIgnoreCase);
                if (gvl)
                {
                    if (objectName != null)
                        CheckObject("GVL", objectName, 1, 1);
                    return;
                }
                if (objectName != null && objectKind != null)
                    CheckObject(objectKind, objectName, 1, 1);
            }

            private void CheckTypeDefinition(StParser.TypeDefinitionContext definition)
            {
                var body = definition.typeBody();
                string kind = body.STRUCT() != null ? "STRUCT"
                    : body.UNION() != null ? "UNION"
                    : body.enumBody() != null ? "ENUM"
                    : "ALIAS";
                var name = new NameToken(definition.name().Start);
                CheckObject(kind, name.Text, name.Line, name.Column);

                if (body.enumBody() != null)
                {
                    foreach (var member in body.enumBody().enumMember())
                        CheckEnumValue(new NameToken(member.name().Start));
                }
                // members of STRUCT and UNION are checked like the variables of a "STRUCT" section
                CheckDeclarations(body.varDeclaration(), "STRUCT", false);
            }

            // ---------------------------------------------------------- variables

            private void CheckVarSection(StParser.VarSectionContext section)
            {
                string keyword = section.varSectionKeyword().Start.Text.ToUpperInvariant();
                bool constant = section.varModifier().Any(m => m.CONSTANT() != null);
                CheckDeclarations(section.varDeclaration(), keyword, constant);
            }

            private void CheckDeclarations(IEnumerable<StParser.VarDeclarationContext> declarations, string section, bool constant)
            {
                bool check = _rules.CheckedSections == null ||
                             _rules.CheckedSections.Any(s => string.Equals(s, section, StringComparison.OrdinalIgnoreCase));
                if (!check)
                    return;

                foreach (var declaration in declarations)
                {
                    var names = declaration.variableNames();
                    if (names == null)
                        continue; // empty declaration ";"
                    // every name of the list; in VAR_CONFIG every part of an instance path ("MAIN.fbX.bIn")
                    foreach (var node in Terminals(names).Where(n => n.Symbol.Type == StLexer.IDENTIFIER))
                        CheckVariable(new NameToken(node.Symbol), declaration.dataType(), section, constant);
                }
            }

            private static IEnumerable<ITerminalNode> Terminals(IParseTree tree)
            {
                if (tree is ITerminalNode terminal)
                {
                    yield return terminal;
                    yield break;
                }
                for (int i = 0; i < tree.ChildCount; i++)
                {
                    foreach (var t in Terminals(tree.GetChild(i)))
                        yield return t;
                }
            }

            /// <summary>The texts of the tokens of a type (for messages).</summary>
            private IEnumerable<string> TypeTokens(StParser.DataTypeContext type)
            {
                if (type == null)
                    yield break;
                for (int i = type.Start.TokenIndex; i <= type.Stop.TokenIndex; i++)
                {
                    if (_tokens[i].Channel == TokenConstants.DefaultChannel)
                        yield return _tokens[i].Text;
                }
            }

            private void CheckVariable(NameToken name, StParser.DataTypeContext type, string section, bool constant)
            {
                if (IsIgnored(name) || CheckIdentifier(name))
                    return;
                var severity = _rules.SeverityOf(NamingRuleIds.VariablePrefix);
                if (severity == NamingSeverity.Off)
                    return;

                var typePrefixes = ResolvePrefixes(type);
                if (typePrefixes == null)
                    return;

                string scope = string.Empty;
                if (_rules.ScopePrefixes != null && _rules.ScopePrefixes.TryGetValue(section, out var sp) && sp != null)
                    scope = sp;

                // local variables of methods may need a marker after the type prefix ("lr_TempAngle")
                bool methodLocal = _headerKind == "METHOD" && (section == "VAR" || section == "VAR_TEMP");
                string marker = methodLocal ? _rules.MethodLocalMarker ?? string.Empty : string.Empty;

                var accepted = typePrefixes.Select(p => scope + p + marker).ToList();
                // nothing configured for this variable (empty type, scope and marker prefix): not checked
                if (accepted.Any(string.IsNullOrEmpty))
                    return;
                if (constant && !string.IsNullOrEmpty(_rules.ConstantPrefix))
                    accepted.Add(scope + _rules.ConstantPrefix);

                // with AllowLeadingUnderscore a leading "_" is not part of the prefix: "_bBusy" is a valid BOOL name
                string lead = _rules.AllowLeadingUnderscore ? new string('_', name.Text.Length - name.Text.TrimStart('_').Length) : string.Empty;
                string text = name.Text.Substring(lead.Length);

                // prefixes that may already be on the name and are replaced by a suggestion
                var strippable = accepted.Concat(typePrefixes).Concat(typePrefixes.Select(p => scope + p))
                    .Where(p => !string.IsNullOrEmpty(p)).Distinct().OrderByDescending(p => p.Length).ToList();

                bool constantUpper = constant && _rules.ConstantNameCase != NameCase.Any;
                string matched = accepted.FirstOrDefault(p => HasPrefix(text, p));
                if (matched != null)
                {
                    string body = text.Substring(matched.Length);
                    if (!constantUpper || MatchesCase(body, _rules.ConstantNameCase))
                        return;
                    string fixedName = lead + matched + ApplyCase(body, _rules.ConstantNameCase);
                    Report(NamingRuleIds.VariablePrefix, severity, name,
                        $"Constant '{name.Text}' should be written in {_rules.ConstantNameCase} after the prefix (e.g. '{fixedName}').", fixedName);
                    return;
                }

                string suggestion = Suggest(text, accepted[0], strippable);
                if (constantUpper)
                    suggestion = accepted[0] + ApplyCase(suggestion.Substring(accepted[0].Length), _rules.ConstantNameCase);
                suggestion = lead + suggestion;
                string typeText = string.Join(" ", TypeTokens(type))
                    .Replace(" [ ", "[").Replace(" ]", "]").Replace(" ( ", "(").Replace(" )", ")").Replace(" .. ", "..").Replace(" . ", ".").Replace(" , ", ", ");
                string expected = string.Join("' or '", accepted.Distinct());
                Report(NamingRuleIds.VariablePrefix, severity, name,
                    $"Variable '{name.Text}' of type {typeText} should start with '{expected}'" +
                    (_rules.RequirePascalCaseAfterPrefix ? " followed by an upper case letter" : "") +
                    $" (e.g. '{suggestion}').", suggestion);
            }

            private static bool MatchesCase(string body, NameCase nameCase)
            {
                if (body.Length == 0)
                    return false;
                switch (nameCase)
                {
                    case NameCase.PascalCase:
                        return char.IsUpper(body[0]) || char.IsDigit(body[0]);
                    case NameCase.UpperCase:
                        return body.All(c => (c >= 'A' && c <= 'Z') || char.IsDigit(c) || c == '_');
                    default:
                        return true;
                }
            }

            /// <summary>"MaxCycles" -> "MAX_CYCLES" (UpperCase) or "maxCycles" -> "MaxCycles" (PascalCase).</summary>
            private static string ApplyCase(string body, NameCase nameCase)
            {
                if (body.Length == 0)
                    return body;
                switch (nameCase)
                {
                    case NameCase.UpperCase:
                        var sb = new System.Text.StringBuilder();
                        for (int i = 0; i < body.Length; i++)
                        {
                            char c = body[i];
                            if (i > 0 && char.IsUpper(c) && (char.IsLower(body[i - 1]) || char.IsDigit(body[i - 1])) && body[i - 1] != '_')
                                sb.Append('_');
                            sb.Append(char.ToUpperInvariant(c));
                        }
                        return sb.ToString();
                    case NameCase.PascalCase:
                        return char.ToUpperInvariant(body[0]) + body.Substring(1);
                    default:
                        return body;
                }
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

            private static string Suggest(string name, string prefix, IEnumerable<string> strippable)
            {
                // 1. a known prefix of this type/scope is already there: keep the rest
                string body = strippable
                    .Where(p => name.StartsWith(p, StringComparison.Ordinal) && name.Length > p.Length)
                    .Select(p => name.Substring(p.Length))
                    .FirstOrDefault();

                // 2. a lower case run followed by an upper case letter or '_' is a wrong prefix: replace it
                if (body == null)
                {
                    int lower = 0;
                    while (lower < name.Length && char.IsLower(name[lower]))
                        lower++;
                    if (lower > 0 && lower < name.Length && (char.IsUpper(name[lower]) || name[lower] == '_'))
                        body = name.Substring(lower);
                    else
                        body = name;
                }
                body = body.TrimStart('_');
                if (body.Length == 0)
                    return prefix + name;
                return prefix + char.ToUpperInvariant(body[0]) + body.Substring(1);
            }

            /// <summary>Accepted type prefixes for the type, or null when the type is unknown (or an implicit enum).</summary>
            private List<string> ResolvePrefixes(StParser.DataTypeContext type)
            {
                if (type == null)
                    return null;
                if (type.ARRAY() != null)
                    return Combine(_rules.ArrayPrefix, _rules.ArrayIncludesElementPrefix, ResolvePrefixes(type.dataType()));
                if (type.POINTER() != null)
                    return Combine(_rules.PointerPrefix, _rules.PointerIncludesTargetPrefix, ResolvePrefixes(type.dataType()));
                if (type.REFERENCE() != null)
                    return Combine(_rules.ReferencePrefix, _rules.ReferenceIncludesTargetPrefix, ResolvePrefixes(type.dataType()));

                // base type name; qualified names (Tc2_Standard.TON) use the last part
                var qualified = type.qualifiedName();
                string typeName = qualified == null
                    ? type.Start.Text
                    : (qualified.memberName().LastOrDefault()?.GetText() ?? qualified.Start.Text);
                return LookupTypePrefix(typeName);
            }

            private static List<string> Combine(string prefix, bool includeInner, List<string> inner)
            {
                prefix = prefix ?? string.Empty;
                if (includeInner && inner == null && prefix.Length == 0)
                    return null; // unknown element / target type and no own prefix: nothing to check
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
                if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(kind) || IsIgnoredName(name) || _ignoredLines.Contains(line))
                    return;
                if (CheckIdentifierText(name, line, column))
                    return;

                kind = kind.ToUpperInvariant();
                string prefix = null;
                _rules.ObjectPrefixes?.TryGetValue(kind, out prefix);
                bool member = kind == "METHOD" || kind == "PROPERTY" || kind == "ACTION";
                string kindName = KindNames.TryGetValue(kind, out var k) ? k : kind;

                // no prefix configured for this kind: nothing to check
                if (string.IsNullOrEmpty(prefix) || (member && SystemMethods.Contains(name)))
                    return;

                bool ok = name.StartsWith(prefix, StringComparison.Ordinal) && name.Length > prefix.Length &&
                          (!_rules.RequirePascalCaseAfterPrefix || char.IsUpper(name[prefix.Length]) || char.IsDigit(name[prefix.Length]));
                if (ok)
                {
                    // methods, properties and actions: no further '_' after the prefix ("m_Start_Axis")
                    string rest = name.Substring(prefix.Length);
                    var memberSeverity = _rules.SeverityOf(NamingRuleIds.MemberName);
                    if (member && !_rules.AllowUnderscoreInMemberNames && rest.IndexOf('_') >= 0 && memberSeverity != NamingSeverity.Off)
                    {
                        string joined = prefix + string.Concat(rest.Split(new[] { '_' }, StringSplitOptions.RemoveEmptyEntries)
                            .Select(p => char.ToUpperInvariant(p[0]) + p.Substring(1)));
                        Report(NamingRuleIds.MemberName, memberSeverity, name, line, column,
                            $"{kindName} '{name}' should not contain '_' after the prefix '{prefix}' (e.g. '{joined}').", joined);
                    }
                    return;
                }

                var sev = _rules.SeverityOf(NamingRuleIds.ObjectPrefix);
                if (sev == NamingSeverity.Off)
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

            private void CheckEnumValue(NameToken value)
            {
                if (IsIgnored(value) || CheckIdentifier(value) || _rules.EnumValueCase == NameCase.Any)
                    return;
                var severity = _rules.SeverityOf(NamingRuleIds.EnumValue);
                if (severity == NamingSeverity.Off || MatchesCase(value.Text, _rules.EnumValueCase))
                    return;
                string suggestion = ApplyCase(value.Text, _rules.EnumValueCase);
                Report(NamingRuleIds.EnumValue, severity, value,
                    $"Enum value '{value.Text}' should be written in {_rules.EnumValueCase} (e.g. '{suggestion}').", suggestion);
            }

            /// <summary>Reports invalid identifiers; returns true when one was reported.</summary>
            private bool CheckIdentifier(NameToken name) =>
                CheckIdentifierText(name.Text, name.Line, name.Column);

            private bool CheckIdentifierText(string name, int line, int column)
            {
                string problem = null;
                if (name.Contains("__"))
                    problem = "must not contain double underscores";
                else if (name.StartsWith("_", StringComparison.Ordinal))
                    problem = _rules.AllowLeadingUnderscore ? null : "must not start with an underscore";
                else if (name.Any(c => c > 127))
                    problem = "should only contain the characters A-Z, a-z, 0-9 and _";
                if (problem == null)
                    return false;
                var severity = _rules.SeverityOf(NamingRuleIds.InvalidIdentifier);
                if (severity != NamingSeverity.Off)
                    Report(NamingRuleIds.InvalidIdentifier, severity, name, line, column, $"Identifier '{name}' {problem}.", null);
                return true;
            }

            private bool IsIgnored(NameToken name) => _ignoredLines.Contains(name.Line) || IsIgnoredName(name.Text);

            private bool IsIgnoredName(string name) => _ignore.Any(r => r.IsMatch(name));

            private void Report(string ruleId, NamingSeverity severity, NameToken name, string message, string suggestion) =>
                Report(ruleId, severity, name.Text, name.Line, name.Column, message, suggestion);

            private void Report(string ruleId, NamingSeverity severity, string name, int line, int column, string message, string suggestion) =>
                _out.Add(new NamingDiagnostic(ruleId, severity, name, message, line, column, suggestion));
        }
    }
}
