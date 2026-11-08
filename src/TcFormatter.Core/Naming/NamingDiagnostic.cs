namespace TcFormatter.Naming
{
    public sealed class NamingDiagnostic
    {
        public NamingDiagnostic(string ruleId, NamingSeverity severity, string name, string message, int line, int column, string suggestion)
        {
            RuleId = ruleId;
            Severity = severity;
            Name = name;
            Message = message;
            Line = line;
            Column = column;
            Suggestion = suggestion;
        }

        public string RuleId { get; }
        public NamingSeverity Severity { get; }

        /// <summary>The offending identifier.</summary>
        public string Name { get; }

        public string Message { get; }

        /// <summary>1-based line inside the checked declaration text.</summary>
        public int Line { get; }

        /// <summary>1-based column inside the checked declaration text.</summary>
        public int Column { get; }

        /// <summary>A name that would satisfy the rule, or null.</summary>
        public string Suggestion { get; }

        public override string ToString() => $"{Line}:{Column} {RuleId} {Severity}: {Message}";
    }
}
