/*
 * Lexer for TwinCAT 3 Structured Text (IEC 61131-3 + ExST).
 *
 * Lossless: every character of the input ends up in exactly one token. Whitespace, line breaks, comments
 * and pragmas go to the HIDDEN channel, unknown characters become ERROR_CHAR. Concatenating the text of all
 * tokens reproduces the source. The formatter relies on that (see docs/adr/0001-antlr-parser.md).
 *
 * Keywords are case-insensitive. Lexer.KindOf classifies tokens by ranges of this file: keywords from IF to ENDTRY,
 * type names from BOOL to SYS_SYSTEM, operators from SET_ASSIGN to HASH. Add new words inside these ranges.
 */
lexer grammar StLexer;

options {
    caseInsensitive = true;
    superClass = StLexerBase;
}

// ------------------------------------------------------------------ trivia (HIDDEN)

NEWLINE       : ('\r\n' | '\r' | '\n') -> channel(HIDDEN);
WS            : [ \t\f\u000B ]+ -> channel(HIDDEN);
LINE_COMMENT  : '//' ~[\r\n]* -> channel(HIDDEN);
// (* ... *) comments nest in TwinCAT. Unterminated comments run to the end of the input.
BLOCK_COMMENT : '(*' (BLOCK_COMMENT | .)*? ('*)' | EOF) -> channel(HIDDEN);
C_COMMENT     : '/*' .*? ('*/' | EOF) -> channel(HIDDEN);
// {attribute ...}, {region ...}, {warning '...'}: a '}' inside a quoted string does not end the pragma.
PRAGMA        : '{' (PRAGMA_STRING | ~[}'"])* ('}' | EOF) -> channel(HIDDEN);

// Strings inside pragmas may span lines ({attribute 'TcLinkTo' := '.A := ...;<newline> .B := ...'}). The end must
// be unambiguous: with an optional closing quote the longest-match rule of the lexer would read
// "{attribute 'a'} x := 'b'" as one pragma.
fragment PRAGMA_STRING
    : '\'' ('$' . | ~['$])* ('\'' | EOF)
    | '"' ('$' . | ~["$])* ('"' | EOF)
    ;

// ------------------------------------------------------------------ keywords

// control flow
IF          : 'IF';
THEN        : 'THEN';
ELSIF       : 'ELSIF';
ELSE        : 'ELSE';
END_IF      : 'END_IF';
CASE        : 'CASE';
OF          : 'OF';
END_CASE    : 'END_CASE';
FOR         : 'FOR';
TO          : 'TO';
BY          : 'BY';
DO          : 'DO';
END_FOR     : 'END_FOR';
WHILE       : 'WHILE';
END_WHILE   : 'END_WHILE';
REPEAT      : 'REPEAT';
UNTIL       : 'UNTIL';
END_REPEAT  : 'END_REPEAT';
EXIT        : 'EXIT';
CONTINUE    : 'CONTINUE';
RETURN      : 'RETURN';
JMP         : 'JMP';

// operators written as words
AND         : 'AND';
OR          : 'OR';
XOR         : 'XOR';
NOT         : 'NOT';
MOD         : 'MOD';
AND_THEN    : 'AND_THEN';
OR_ELSE     : 'OR_ELSE';

// variable sections
VAR          : 'VAR';
VAR_INPUT    : 'VAR_INPUT';
VAR_OUTPUT   : 'VAR_OUTPUT';
VAR_IN_OUT   : 'VAR_IN_OUT';
VAR_GLOBAL   : 'VAR_GLOBAL';
VAR_TEMP     : 'VAR_TEMP';
VAR_STAT     : 'VAR_STAT';
VAR_EXTERNAL : 'VAR_EXTERNAL';
VAR_INST     : 'VAR_INST';
VAR_CONFIG   : 'VAR_CONFIG';
END_VAR      : 'END_VAR';
CONSTANT     : 'CONSTANT';
RETAIN       : 'RETAIN';
PERSISTENT   : 'PERSISTENT';
AT           : 'AT';

// POUs
PROGRAM            : 'PROGRAM';
END_PROGRAM        : 'END_PROGRAM';
FUNCTION           : 'FUNCTION';
END_FUNCTION       : 'END_FUNCTION';
FUNCTION_BLOCK     : 'FUNCTION_BLOCK';
END_FUNCTION_BLOCK : 'END_FUNCTION_BLOCK';
METHOD             : 'METHOD';
END_METHOD         : 'END_METHOD';
PROPERTY           : 'PROPERTY';
END_PROPERTY       : 'END_PROPERTY';
INTERFACE          : 'INTERFACE';
END_INTERFACE      : 'END_INTERFACE';
ACTION             : 'ACTION';
END_ACTION         : 'END_ACTION';
EXTENDS            : 'EXTENDS';
IMPLEMENTS         : 'IMPLEMENTS';
ABSTRACT           : 'ABSTRACT';
FINAL              : 'FINAL';
PUBLIC             : 'PUBLIC';
PRIVATE            : 'PRIVATE';
PROTECTED          : 'PROTECTED';
INTERNAL           : 'INTERNAL';

// types
TYPE       : 'TYPE';
END_TYPE   : 'END_TYPE';
STRUCT     : 'STRUCT';
END_STRUCT : 'END_STRUCT';
UNION      : 'UNION';
END_UNION  : 'END_UNION';
ARRAY      : 'ARRAY';
POINTER    : 'POINTER';
REFERENCE  : 'REFERENCE';

// literals / special
TRUE             : 'TRUE';
FALSE            : 'FALSE';
THIS             : 'THIS';
SUPER            : 'SUPER';
NEW              : '__NEW';
DELETE           : '__DELETE';
QUERYINTERFACE   : '__QUERYINTERFACE';
QUERYPOINTER     : '__QUERYPOINTER';
ISVALIDREF       : '__ISVALIDREF';
TRY              : '__TRY';
CATCH            : '__CATCH';
FINALLY          : '__FINALLY';
ENDTRY           : '__ENDTRY';

// ------------------------------------------------------------------ elementary type names

BOOL           : 'BOOL';
BIT            : 'BIT';
BYTE           : 'BYTE';
WORD           : 'WORD';
DWORD          : 'DWORD';
LWORD          : 'LWORD';
SINT           : 'SINT';
USINT          : 'USINT';
INT            : 'INT';
UINT           : 'UINT';
DINT           : 'DINT';
UDINT          : 'UDINT';
LINT           : 'LINT';
ULINT          : 'ULINT';
REAL           : 'REAL';
LREAL          : 'LREAL';
STRING         : 'STRING';
WSTRING        : 'WSTRING';
TIME           : 'TIME';
LTIME          : 'LTIME';
DATE           : 'DATE';
LDATE          : 'LDATE';
TIME_OF_DAY    : 'TIME_OF_DAY';
TOD            : 'TOD';
LTOD           : 'LTOD';
DATE_AND_TIME  : 'DATE_AND_TIME';
DT             : 'DT';
LDT            : 'LDT';
ANY            : 'ANY';
ANY_NUM        : 'ANY_NUM';
ANY_INT        : 'ANY_INT';
ANY_REAL       : 'ANY_REAL';
ANY_BIT        : 'ANY_BIT';
ANY_STRING     : 'ANY_STRING';
ANY_DATE       : 'ANY_DATE';
ANY_ELEMENTARY : 'ANY_ELEMENTARY';
ANY_MAGNITUDE  : 'ANY_MAGNITUDE';
ANY_DERIVED    : 'ANY_DERIVED';
XINT           : 'XINT';
UXINT          : 'UXINT';
XWORD          : 'XWORD';
PVOID          : 'PVOID';
SYS_XINT       : '__XINT';
SYS_UXINT      : '__UXINT';
SYS_XWORD      : '__XWORD';
SYS_SYSTEM     : '__SYSTEM';

// ------------------------------------------------------------------ literals

// Durations: T#1s500ms, LTIME#1ns, T#-5s. Listed before TYPED_LITERAL so that TIME#5 is a duration.
DURATION_LITERAL : ('T' | 'TIME' | 'LT' | 'LTIME') '#' [+-]? [\p{L}\p{Nd}_.:]+;
// Dates and times of day: D#2024-01-31, TOD#12:30:15.5, DT#2024-01-01-12:00:00
DATE_LITERAL     : ('D' | 'DATE' | 'LD' | 'LDATE' | 'TOD' | 'TIME_OF_DAY' | 'LTOD' | 'DT' | 'DATE_AND_TIME' | 'LDT')
                   '#' [+-]? [\p{L}\p{Nd}_.:-]+;
// Typed literals: INT#5, INT#-5, INT#16#7F, REAL#1.5, BOOL#TRUE. E_State#Idle is IDENTIFIER HASH IDENTIFIER.
TYPED_LITERAL    : ELEMENTARY_TYPE_NAME '#' [+-]? (NUMBER_BODY | 'TRUE' | 'FALSE');
NUMBER           : NUMBER_BODY;

fragment NUMBER_BODY
    : [0-9] [0-9_]* ( '#' [0-9a-f] [0-9a-f_]*                            // based: 16#FF, 2#1010_1010
                    | ('.' [0-9] [0-9_]*)? ('e' [+-]? [0-9]+)?           // 1_000, 1.5, 1.5E-3 (not the range '..')
                    )
    ;

fragment ELEMENTARY_TYPE_NAME
    : 'BOOL' | 'BIT' | 'BYTE' | 'WORD' | 'DWORD' | 'LWORD'
    | 'SINT' | 'USINT' | 'INT' | 'UINT' | 'DINT' | 'UDINT' | 'LINT' | 'ULINT'
    | 'REAL' | 'LREAL' | 'STRING' | 'WSTRING'
    | 'TIME' | 'LTIME' | 'DATE' | 'LDATE' | 'TIME_OF_DAY' | 'TOD' | 'LTOD' | 'DATE_AND_TIME' | 'DT' | 'LDT'
    | 'ANY' | 'ANY_NUM' | 'ANY_INT' | 'ANY_REAL' | 'ANY_BIT' | 'ANY_STRING' | 'ANY_DATE' | 'ANY_ELEMENTARY'
    | 'ANY_MAGNITUDE' | 'ANY_DERIVED'
    | 'XINT' | 'UXINT' | 'XWORD' | 'PVOID' | '__XINT' | '__UXINT' | '__XWORD' | '__SYSTEM'
    ;

// '...' and "..." with $ escapes ($', $N, $$). TwinCAT accepts line breaks inside a string; a string without
// closing quote stops at the end of the line.
STRING_LITERAL  : '\'' ('$' . | ~['$])* '\'' | '\'' ('$' . | ~['$\r\n])*;
WSTRING_LITERAL : '"' ('$' . | ~["$])* '"' | '"' ('$' . | ~["$\r\n])*;

// %I*, %QX0.1, %MW10, %IB*
DIRECT_ADDRESS : '%' [iqm] [xbwdl]? ('*' | [0-9]+ ('.' [0-9]+)*)?;

// ------------------------------------------------------------------ operators

// Set / reset / reference assignment: x S= y; x R= y; ref REF= x; only directly after an operand
// (so that "IF S=5" stays a comparison) and not in front of '>' (so that "S=>" stays an output).
SET_ASSIGN    : 'S=' {IsSetResetAssignment()}?;
RESET_ASSIGN  : 'R=' {IsSetResetAssignment()}?;
REF_ASSIGN    : 'REF=' {IsSetResetAssignment()}?;

ASSIGN         : ':=';
OUTPUT_ASSIGN  : '=>';
ATTEMPT_ASSIGN : '?=';
LE             : '<=';
GE             : '>=';
NE             : '<>';
POWER          : '**';
DOTDOT         : '..';
PLUS           : '+';
MINUS          : '-';
STAR           : '*';
SLASH          : '/';
LT             : '<';
GT             : '>';
EQ             : '=';
AMPERSAND      : '&';
LPAREN         : '(';
RPAREN         : ')';
LBRACKET       : '[';
RBRACKET       : ']';
COMMA          : ',';
SEMI           : ';';
COLON          : ':';
DOT            : '.';
CARET          : '^';
HASH           : '#';

IDENTIFIER : [\p{L}_] [\p{L}\p{Nd}_]*;

// Anything else (e.g. '?', '$', '@'): the parser reports a syntax error, the lexer stays lossless.
ERROR_CHAR : .;
