using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using TcFormatter.Formatting;

namespace TcFormatter.Configuration
{
    /// <summary>
    /// Imports a settings file exported from STweep (JSON: { "Settings": { section: { key: value } }, "Version": ... }).
    /// Every key is either mapped onto a TcFormatter option or reported in the notes; nothing is dropped silently.
    /// The mapping is based on the key names of the exported file, the public STweep documentation and the
    /// value meanings confirmed by the user (IndentStyle 0 = spaces, line break mode 0 = "break of long or multiline").
    /// </summary>
    public static class StweepImport
    {
        private delegate void Setter(FormatterOptions f, Value v);

        /// <summary>A setting value of the STweep file.</summary>
        private sealed class Value
        {
            public Value(string key, object raw)
            {
                Key = key;
                Raw = raw;
            }

            public string Key { get; }
            public object Raw { get; }

            public bool Bool => Raw is bool b ? b : throw new FormatException($"'{Key}' should be true or false.");
            public int Int => Raw is double d ? (int)d : throw new FormatException($"'{Key}' should be a number.");
        }

        /// <summary>STweep line break mode values (0 confirmed; 1 = "break always" per the release notes).</summary>
        private static LineBreakMode Mode(Value v)
        {
            switch (v.Int)
            {
                case 0: return LineBreakMode.LongOrMultiline;
                case 1: return LineBreakMode.Always;
                default: throw new FormatException($"'{v.Key}' = {v.Int} is not a known line break mode.");
            }
        }

        private static readonly Dictionary<string, Setter> Map = new Dictionary<string, Setter>(StringComparer.OrdinalIgnoreCase)
        {
            // ---------------------------------------------------------------- BlankLines
            ["BlankLines.BlankLinesAfterMultiLineCase"] = (f, v) => f.BlankLinesAfterMultiLineCase = v.Int,
            ["BlankLines.BlankLinesAfterSingleLineCase"] = (f, v) => f.BlankLinesAfterSingleLineCase = v.Int,
            ["BlankLines.BlankLinesAfterMultiLineComment"] = (f, v) => f.BlankLinesAfterMultiLineComment = v.Int,
            ["BlankLines.BlankLinesAfterSingleLineComment"] = (f, v) => f.BlankLinesAfterSingleLineComment = v.Int,
            ["BlankLines.BlankLinesBeforeMultiLineComment"] = (f, v) => f.BlankLinesBeforeMultiLineComment = v.Int,
            ["BlankLines.BlankLinesBeforeSingleLineComment"] = (f, v) => f.BlankLinesBeforeSingleLineComment = v.Int,
            ["BlankLines.BlankLinesAfterMultiLineDeclaration"] = (f, v) => f.BlankLinesAfterMultiLineDeclaration = v.Int,
            ["BlankLines.BlankLinesAfterSingleLineDeclaration"] = (f, v) => f.BlankLinesAfterSingleLineDeclaration = v.Int,
            ["BlankLines.BlankLinesBeforeMultiLineDeclaration"] = (f, v) => f.BlankLinesBeforeMultiLineDeclaration = v.Int,
            ["BlankLines.BlankLinesBeforeSingleLineDeclaration"] = (f, v) => f.BlankLinesBeforeSingleLineDeclaration = v.Int,
            ["BlankLines.BlankLinesAfterMultiLineStatement"] = (f, v) => f.BlankLinesAfterMultiLineStatement = v.Int,
            ["BlankLines.BlankLinesAfterSingleLineStatement"] = (f, v) => f.BlankLinesAfterSingleLineStatement = v.Int,
            ["BlankLines.BlankLinesBeforeMultiLineStatement"] = (f, v) => f.BlankLinesBeforeMultiLineStatement = v.Int,
            ["BlankLines.BlankLinesBeforeSingleLineStatement"] = (f, v) => f.BlankLinesBeforeSingleLineStatement = v.Int,
            ["BlankLines.BlankLinesAfterStatementWithBody"] = (f, v) => f.BlankLinesAfterStatementWithBody = v.Int,
            ["BlankLines.BlankLinesBeforeStatementWithBody"] = (f, v) => f.BlankLinesBeforeStatementWithBody = v.Int,
            ["BlankLines.BlankLinesAfterVarBlocks"] = (f, v) => f.BlankLinesAfterVarBlocks = v.Int,
            ["BlankLines.BlankLinesAtImplementationFileStart"] = (f, v) => f.BlankLinesAtFileStart = v.Int,
            ["BlankLines.MaximumBlankLinesToKeepInImplementationCode"] = (f, v) => f.MaxConsecutiveBlankLines = v.Int,

            // ---------------------------------------------------------------- LineBreaksAndWrapping
            ["LineBreaksAndWrapping.InvocationParametersLineBreakMode"] = (f, v) => f.CallLineBreakMode = Mode(v),
            ["LineBreaksAndWrapping.StructureInitializerParametersLineBreakMode"] = (f, v) => f.InitializerLineBreakMode = Mode(v),
            ["LineBreaksAndWrapping.FunctionBlockInitializerParametersLineBreakMode"] = (f, v) => f.InitializerLineBreakMode = Mode(v),
            ["LineBreaksAndWrapping.ArrayInitializerParametersLineBreakMode"] = (f, v) => f.InitializerLineBreakMode = Mode(v),
            ["LineBreaksAndWrapping.ImplicitEnumLineBreakMode"] = (f, v) => f.EnumLineBreakMode = Mode(v),
            ["LineBreaksAndWrapping.MaxInvocationParametersOnSingleLine"] = (f, v) => f.MaxCallArgumentsOnSingleLine = v.Int,
            ["LineBreaksAndWrapping.MaxStructureInitializerParametersOnSingleLine"] = (f, v) => f.MaxInitializerArgumentsOnSingleLine = v.Int,
            ["LineBreaksAndWrapping.MaxFunctionBlockInitializerParametersOnSingleLine"] = (f, v) => f.MaxInitializerArgumentsOnSingleLine = v.Int,
            ["LineBreaksAndWrapping.MaxArrayInitializersOnSingleLine"] = (f, v) => f.MaxInitializerArgumentsOnSingleLine = v.Int,
            ["LineBreaksAndWrapping.MaxImplicitEnumsOnSingleLine"] = (f, v) => f.MaxEnumValuesOnSingleLine = v.Int,
            ["LineBreaksAndWrapping.KeepExistingLineBreaksInImplementationCode"] = (f, v) => f.KeepExistingLineBreaks = v.Bool,
            ["LineBreaksAndWrapping.LinebreakBeforeThen"] = (f, v) => f.LineBreakBeforeThen = v.Bool,
            ["LineBreaksAndWrapping.LinebreakBeforeDoInForLoop"] = (f, v) => f.LineBreakBeforeDo = v.Bool,
            ["LineBreaksAndWrapping.LineBreakBeforeStatementsInCase"] = (f, v) => f.CaseStatementOnNewLine = v.Bool,
            ["LineBreaksAndWrapping.LineBreakAfterTypeKeyWord"] = (f, v) => f.LineBreakAfterTypeKeyword = v.Bool,
            ["LineBreaksAndWrapping.PlaceSimpleIfStatementsOnSingleLine"] = (f, v) => f.SimpleIfOnSingleLine = v.Bool,
            ["LineBreaksAndWrapping.PlaceStructKeywordOnNewLine"] = (f, v) => f.StructOnNewLine = v.Bool,
            ["LineBreaksAndWrapping.WrapLongLinesAtInImplementationCode"] = (f, v) => f.MaxLineLength = v.Int,
            ["LineBreaksAndWrapping.WrapLongLinesInImplementationCode"] = (f, v) => { if (!v.Bool) f.MaxLineLength = 0; },
            ["LineBreaksAndWrapping.LineBreakAfterLeftParenthesisInInvocation"] = (f, v) =>
                f.CallWrapStyle = v.Bool ? CallWrapStyle.OnePerLineIndented : CallWrapStyle.AlignWithOpeningParenthesis,
            ["LineBreaksAndWrapping.LineBreakBeforeRightParenthesisInInvocation"] = (f, v) => f.WrappedCallClosingParenthesisOnNewLine = v.Bool,
            ["LineBreaksAndWrapping.LineBreakAfterLeftParenthesisInStructureInitializer"] = (f, v) => f.LineBreakAfterInitializerOpenParenthesis = v.Bool,
            ["LineBreaksAndWrapping.LineBreakAfterLeftParenthesisInFunctionBlockInitializer"] = (f, v) => f.LineBreakAfterInitializerOpenParenthesis = v.Bool,
            ["LineBreaksAndWrapping.LineBreakAfterLeftBracketInArrayInitializer"] = (f, v) => f.LineBreakAfterInitializerOpenParenthesis = v.Bool,
            ["LineBreaksAndWrapping.LineBreakBeforeRightParenthesisInStructureInitializer"] = (f, v) => f.InitializerClosingParenthesisOnNewLine = v.Bool,
            ["LineBreaksAndWrapping.LineBreakBeforeRightParenthesisInFunctionBlockInitializer"] = (f, v) => f.InitializerClosingParenthesisOnNewLine = v.Bool,
            ["LineBreaksAndWrapping.LineBreakBeforeRightBracketInArrayInitializer"] = (f, v) => f.InitializerClosingParenthesisOnNewLine = v.Bool,
            ["LineBreaksAndWrapping.LineBreakBeforeLeftParenthesisInImplicitEnum"] = (f, v) => f.LineBreakBeforeEnumOpenParenthesis = v.Bool,
            ["LineBreaksAndWrapping.LineBreakAfterLeftParenthesisInImplicitEnum"] = (f, v) => f.LineBreakAfterEnumOpenParenthesis = v.Bool,
            ["LineBreaksAndWrapping.LineBreakBeforeRightParenthesisInImplicitEnum"] = (f, v) => f.LineBreakBeforeEnumCloseParenthesis = v.Bool,

            // ---------------------------------------------------------------- MiscellaneousSettings
            ["MiscellaneousSettings.KeywordsToUpper"] = (f, v) => f.KeywordCasing = v.Bool ? CasingStyle.Upper : CasingStyle.Preserve,

            // ---------------------------------------------------------------- Spaces
            ["Spaces.AfterComma"] = (f, v) => f.SpaceAfterComma = v.Bool,
            ["Spaces.BeforeComma"] = (f, v) => f.SpaceBeforeComma = v.Bool,
            ["Spaces.AfterVariableDeclarationColon"] = (f, v) => f.SpaceAfterColon = v.Bool,
            ["Spaces.BeforeVariableDeclarationColon"] = (f, v) => f.SpaceBeforeColon = v.Bool,
            ["Spaces.AroundAssignmentOperator"] = (f, v) => f.SpaceAroundAssignment = v.Bool,
            ["Spaces.AroundAssignmentOperatorInParameterAssignInInvocationCall"] = (f, v) => f.SpaceBeforeArgumentAssignment = v.Bool,
            ["Spaces.AroundAssignmentOperatorInParameterAssignInStructureAndFunctionBlockInitializers"] = (f, v) => f.SpaceBeforeArgumentAssignment = v.Bool,
            ["Spaces.AroundAdditiveOperator"] = (f, v) => f.SpaceAroundOperators = v.Bool,
            ["Spaces.AroundComparisonOperator"] = (f, v) => f.SpaceAroundOperators = v.Bool,
            ["Spaces.AroundEqualityOperator"] = (f, v) => f.SpaceAroundOperators = v.Bool,
            ["Spaces.AroundTermOperator"] = (f, v) => f.SpaceAroundOperators = v.Bool,
            ["Spaces.BeforeInvocationParentheses"] = (f, v) => f.SpaceBeforeCallParenthesis = v.Bool,
            ["Spaces.BeforeArrayBrackets"] = (f, v) => f.SpaceAfterArrayKeyword = v.Bool,
            ["Spaces.BetweenNestedParentheses"] = (f, v) => f.SpaceBetweenNestedParentheses = v.Bool,
            ["Spaces.InsideExpressionParentheses"] = (f, v) => f.SpaceInsideParentheses = v.Bool,
            ["Spaces.InsideInvocationParentheses"] = (f, v) => f.SpaceInsideParentheses |= v.Bool,
            ["Spaces.InsideArrayBrackets"] = (f, v) => f.SpaceInsideBrackets = v.Bool,

            // ---------------------------------------------------------------- TabsIndentAlignment
            ["TabsIndentAlignment.IndentStyle"] = (f, v) => f.IndentStyle = v.Int == 0 ? IndentStyle.Spaces : IndentStyle.Tabs,
            ["TabsIndentAlignment.IndentSize"] = (f, v) => f.IndentSize = v.Int,
            ["TabsIndentAlignment.TabSize"] = (f, v) => { }, // = IndentSize in TcFormatter; differences are reported below
            ["TabsIndentAlignment.ContinuousLineIndentMultiplier"] = (f, v) => f.ContinuationIndent = v.Int,
            ["TabsIndentAlignment.IndentCasesInCaseStatement"] = (f, v) => f.IndentCaseLabels = v.Bool,
            ["TabsIndentAlignment.IndentStatementsInCase"] = (f, v) => f.IndentCaseBody = v.Bool,
            ["TabsIndentAlignment.IndentElseCaseInCaseStatement"] = (f, v) => f.IndentCaseElse = v.Bool,
            ["TabsIndentAlignment.IndentThenInIfStatement"] = (f, v) => f.IndentThen = v.Bool,
            ["TabsIndentAlignment.IndentDoInIfForLoop"] = (f, v) => f.IndentDo = v.Bool,
            ["TabsIndentAlignment.IndentDerivedDataTypesInTypeDeclaration"] = (f, v) => f.IndentTypeDeclaration = v.Bool,
            ["TabsIndentAlignment.IndentLastCommentBeforeCase"] = (f, v) => f.CommentBeforeCaseLabelAtLabelLevel = !v.Bool,
            ["TabsIndentAlignment.AlignSimilarCodeInColumns.VariableDeclaration"] = (f, v) => f.AlignDeclarationColons = v.Bool,
            ["TabsIndentAlignment.AlignSimilarCodeInColumns.VariableInitializers"] = (f, v) => f.AlignDeclarationInitializers = v.Bool,
            ["TabsIndentAlignment.AlignSimilarCodeInColumns.EnumMemberInitializers"] = (f, v) => f.AlignEnumValues = v.Bool,
            ["TabsIndentAlignment.AlignSimilarCodeInColumns.Assignments"] = (f, v) => f.AlignConsecutiveAssignments = v.Bool,
            ["TabsIndentAlignment.AlignSimilarCodeInColumns.EndOfLineComments"] = (f, v) =>
            {
                var alignment = v.Bool ? TrailingCommentAlignment.AlignInBlock : TrailingCommentAlignment.None;
                f.DeclarationCommentAlignment = alignment;
                f.CodeCommentAlignment = alignment;
            },
            ["TabsIndentAlignment.AlignMultilineConstructs.InvocationCallVariableAssignments"] = (f, v) => f.AlignWrappedArguments = v.Bool,
            ["TabsIndentAlignment.AlignMultilineConstructs.StructureInitializerParameterAssignments"] = (f, v) => f.AlignWrappedArguments |= v.Bool,
            ["TabsIndentAlignment.AlignMultilineConstructs.FunctionBlockInitializerParameterAssignments"] = (f, v) => f.AlignWrappedArguments |= v.Bool,
        };

        /// <summary>Settings without a TcFormatter counterpart that need no message when they have this value.</summary>
        private static readonly Dictionary<string, object> HarmlessValues = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
        {
            ["Spaces.AroundComment"] = true,               // TcFormatter always separates comments by a space
            ["Spaces.AroundDot"] = false,                  // never spaces around '.'
            ["Spaces.BeforeSemiColon"] = false,            // never a space before ';'
            ["Spaces.SpacesAroundPragma"] = true,          // pragmas are always separated
            ["Spaces.BeforeArrayInitializerBrackets"] = false,
            ["Spaces.BeforeFunctionBlockInitializerParentheses"] = false,
            ["Spaces.BeforeStructureInitializerParentheses"] = false,
            ["Spaces.BeforeSubRangeParentheses"] = false,
            ["Spaces.InsideArrayInitializerBrackets"] = false,
            ["Spaces.InsideFunctionBlockInitializerParentheses"] = false,
            ["Spaces.InsideStructureInitializerParentheses"] = false,
            ["Spaces.InsideSubRangeParentheses"] = false,
            ["BlankLines.BlankLinesAtImplementationFileEnd"] = 0.0,
            ["LineBreaksAndWrapping.LinebreakAfterIf"] = false,
            ["LineBreaksAndWrapping.WrapBeforeComma"] = false,
            ["LineBreaksAndWrapping.PreferWrapBeforeBinaryExpressionOperator"] = false,
            ["LineBreaksAndWrapping.LineBreakBeforeLeftParenthesisInInvocation"] = false,
            ["LineBreaksAndWrapping.LineBreakBeforeLeftParenthesisInStructureInitializer"] = false,
            ["LineBreaksAndWrapping.LineBreakBeforeLeftParenthesisInFunctionBlockInitializer"] = false,
            ["LineBreaksAndWrapping.LineBreakBeforeLeftBracketInArrayInitializer"] = false,
            ["TabsIndentAlignment.IndentLastCommentBeforeElse"] = false,
            ["TabsIndentAlignment.AlignMultilineConstructs.MultilineConstructsAlignmentStyleWhenTabsAreUsed"] = 0.0,
            ["TabsIndentAlignment.AlignSimilarCodeInColumns.AlignSimilarCodeInColumnsAlignmentStyleWhenTabsAreUsed"] = 0.0,
            ["TabsIndentAlignment.AlignSimilarCodeInColumns.AddressAssignments"] = false,
            ["TabsIndentAlignment.AlignMultilineConstructs.ArrayInitializers"] = false,
            ["TabsIndentAlignment.AlignMultilineConstructs.ChainedBinaryExpression"] = false,
            ["TabsIndentAlignment.AlignMultilineConstructs.FunctionBlockInitializerParameters"] = false,
            ["TabsIndentAlignment.AlignMultilineConstructs.InvocationCallParameters"] = false,
            ["TabsIndentAlignment.AlignMultilineConstructs.StructureInitializerParameters"] = false,
        };

        /// <summary>Keys whose value is applied to one shared TcFormatter option; differing values are reported.</summary>
        private static readonly string[][] SharedTargets =
        {
            new[] { "LineBreaksAndWrapping.StructureInitializerParametersLineBreakMode", "LineBreaksAndWrapping.FunctionBlockInitializerParametersLineBreakMode", "LineBreaksAndWrapping.ArrayInitializerParametersLineBreakMode" },
            new[] { "LineBreaksAndWrapping.MaxStructureInitializerParametersOnSingleLine", "LineBreaksAndWrapping.MaxFunctionBlockInitializerParametersOnSingleLine", "LineBreaksAndWrapping.MaxArrayInitializersOnSingleLine" },
            new[] { "Spaces.AroundAdditiveOperator", "Spaces.AroundComparisonOperator", "Spaces.AroundEqualityOperator", "Spaces.AroundTermOperator" },
            new[] { "Spaces.AroundAssignmentOperatorInParameterAssignInInvocationCall", "Spaces.AroundAssignmentOperatorInParameterAssignInStructureAndFunctionBlockInitializers" },
            new[] { "TabsIndentAlignment.IndentSize", "TabsIndentAlignment.TabSize" },
        };

        /// <summary>Converts the STweep settings into a TcFormatter configuration (naming rules keep their defaults).</summary>
        public static TcFormatConfig Convert(string json, out List<string> notes)
        {
            notes = new List<string>();
            object document;
            try
            {
                document = Json.Parse(json);
            }
            catch (ConfigException ex)
            {
                throw new ConfigException("Not a STweep settings file: " + ex.Message);
            }

            var root = document as List<KeyValuePair<string, object>>;
            var settings = root?.FirstOrDefault(p => string.Equals(p.Key, "Settings", StringComparison.OrdinalIgnoreCase)).Value
                as List<KeyValuePair<string, object>>;
            if (settings == null)
                throw new ConfigException("Not a STweep settings file: the object 'Settings' is missing.");

            var version = root.FirstOrDefault(p => string.Equals(p.Key, "Version", StringComparison.OrdinalIgnoreCase)).Value as string;
            if (version != null && !version.StartsWith("4.", StringComparison.Ordinal))
                notes.Add($"The file was written by STweep {version}; the import was made for STweep 4.x, please check the result.");

            var values = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            Flatten(settings, string.Empty, values);

            // the TwinCAT default of TcFormatter is tabs; STweep files describe their own style completely
            var config = new TcFormatConfig();
            var format = config.Format;

            foreach (var pair in values)
            {
                if (Map.TryGetValue(pair.Key, out var setter))
                {
                    try
                    {
                        setter(format, new Value(pair.Key, pair.Value));
                    }
                    catch (FormatException ex)
                    {
                        notes.Add($"{pair.Key}: {ex.Message} The setting was ignored.");
                    }
                }
                else if (HarmlessValues.TryGetValue(pair.Key, out var harmless))
                {
                    if (!Equals(harmless, pair.Value))
                        notes.Add($"{pair.Key} = {Show(pair.Value)} is not supported by TcFormatter (it behaves like {Show(harmless)}).");
                }
                else
                {
                    notes.Add($"{pair.Key} = {Show(pair.Value)} has no TcFormatter counterpart and was ignored.");
                }
            }

            foreach (var group in SharedTargets)
            {
                var present = group.Where(values.ContainsKey).Select(k => (Key: k, Value: Show(values[k]))).ToList();
                if (present.Select(p => p.Value).Distinct().Count() > 1)
                {
                    notes.Add("TcFormatter has one setting for " + string.Join(", ", present.Select(p => $"{p.Key} = {p.Value}")) +
                              "; the last value was used.");
                }
            }
            return config;
        }

        private static void Flatten(List<KeyValuePair<string, object>> section, string prefix, Dictionary<string, object> values)
        {
            foreach (var pair in section)
            {
                string key = prefix + pair.Key;
                if (pair.Value is List<KeyValuePair<string, object>> child)
                    Flatten(child, key + ".", values);
                else
                    values[key] = pair.Value;
            }
        }

        private static string Show(object value)
        {
            switch (value)
            {
                case bool b: return b ? "true" : "false";
                case double d: return d.ToString(CultureInfo.InvariantCulture);
                case null: return "null";
                default: return value.ToString();
            }
        }
    }
}
