/*
 * Parser for TwinCAT 3 Structured Text (IEC 61131-3 + ExST).
 *
 * TwinCAT stores the declaration and the implementation of an object separately, so there are two entry rules:
 *   declarationPart    - POU header, VAR sections, TYPE definitions, GVL, interface (CodePart.Declaration)
 *   implementationPart - statements (CodePart.Implementation)
 *
 * Comments, pragmas and whitespace are on the HIDDEN channel and invisible here.
 * Operator precedence follows IEC 61131-3 (lowest first): OR, XOR, AND, = <>, < > <= >=, + -, * / MOD,
 * unary - + NOT, **, postfix (call, index, member, dereference).
 */
parser grammar StParser;

options {
    tokenVocab = StLexer;
}

// ================================================================== entry rules

declarationPart
    : declarationItem* EOF
    ;

implementationPart
    : statementList EOF
    ;

declarationItem
    : pouHeader
    | modifier                 // declaration of a property accessor (Get / Set): "PUBLIC" followed by VAR sections
    | varSection
    | typeDeclaration
    | SEMI
    ;

// ================================================================== declarations

pouHeader
    : PROGRAM name
    | FUNCTION_BLOCK modifier* name (EXTENDS qualifiedName)? (IMPLEMENTS qualifiedName (COMMA qualifiedName)*)?
    | FUNCTION modifier* name (COLON dataType)?
    | METHOD modifier* name (COLON dataType)?
    | PROPERTY modifier* name COLON dataType
    | INTERFACE modifier* name (EXTENDS qualifiedName (COMMA qualifiedName)*)?
    | ACTION name COLON?
    ;

modifier
    : ABSTRACT | FINAL | PUBLIC | PRIVATE | PROTECTED | INTERNAL
    ;

varSection
    : varSectionKeyword varModifier* varDeclaration* END_VAR SEMI?
    ;

varSectionKeyword
    : VAR | VAR_INPUT | VAR_OUTPUT | VAR_IN_OUT | VAR_GLOBAL | VAR_TEMP | VAR_STAT
    | VAR_EXTERNAL | VAR_INST | VAR_CONFIG
    ;

varModifier
    : CONSTANT | RETAIN | PERSISTENT
    ;

varDeclaration
    : variableNames (AT DIRECT_ADDRESS)? COLON (dataType | enumBody) initialization? SEMI   // enumBody: implicit enum
    | SEMI
    ;

variableNames
    : qualifiedName (COMMA qualifiedName)*
    ;

initialization
    : (ASSIGN | REF_ASSIGN) expression
    ;

typeDeclaration
    : TYPE typeDefinition+ END_TYPE SEMI?
    ;

typeDefinition
    : name (EXTENDS qualifiedName)? COLON typeBody SEMI?
    ;

typeBody
    : STRUCT (EXTENDS qualifiedName)? varDeclaration* END_STRUCT
    | UNION varDeclaration* END_UNION
    | enumBody
    | dataType initialization?
    ;

enumBody
    : LPAREN enumMember (COMMA enumMember)* COMMA? RPAREN dataType? initialization?
    ;

enumMember
    : name (ASSIGN expression)?
    ;

dataType
    : ARRAY LBRACKET arrayRange (COMMA arrayRange)* RBRACKET OF dataType
    | (POINTER | REFERENCE) TO dataType
    | (STRING | WSTRING) ((LPAREN expression RPAREN) | (LBRACKET expression RBRACKET))?
    | elementaryType (LPAREN expression DOTDOT expression RPAREN)?
    | qualifiedName (LPAREN argumentList? RPAREN)?
    ;

arrayRange
    : expression DOTDOT expression
    | STAR
    ;

elementaryType
    : BOOL | BIT | BYTE | WORD | DWORD | LWORD
    | SINT | USINT | INT | UINT | DINT | UDINT | LINT | ULINT
    | REAL | LREAL
    | TIME | LTIME | DATE | LDATE | TIME_OF_DAY | TOD | LTOD | DATE_AND_TIME | DT | LDT
    | ANY | ANY_NUM | ANY_INT | ANY_REAL | ANY_BIT | ANY_STRING | ANY_DATE | ANY_ELEMENTARY
    | ANY_MAGNITUDE | ANY_DERIVED
    | XINT | UXINT | XWORD | PVOID | SYS_XINT | SYS_UXINT | SYS_XWORD
    ;

// ================================================================== statements

statementList
    : statement*
    ;

statement
    : labelStatement
    | unlabeledStatement
    ;

// Inside a CASE branch "name:" starts the next branch, so labels are only allowed outside of CASE branches.
caseBranchStatements
    : unlabeledStatement*
    ;

labelStatement
    : name COLON
    ;

unlabeledStatement
    : assignmentStatement
    | expressionStatement
    | ifStatement
    | caseStatement
    | forStatement
    | whileStatement
    | repeatStatement
    | tryStatement
    | jumpStatement
    | EXIT SEMI
    | CONTINUE SEMI
    | RETURN SEMI
    | SEMI
    ;

assignmentStatement
    : expression assignmentOperator expression SEMI
    ;

assignmentOperator
    : ASSIGN | SET_ASSIGN | RESET_ASSIGN | REF_ASSIGN | ATTEMPT_ASSIGN
    ;

expressionStatement
    : expression SEMI
    ;

jumpStatement
    : JMP name SEMI
    ;

ifStatement
    : IF expression THEN statementList
      (ELSIF expression THEN statementList)*
      (ELSE statementList)?
      END_IF SEMI?
    ;

caseStatement
    : CASE expression OF caseBranch* (ELSE statementList)? END_CASE SEMI?
    ;

caseBranch
    : caseLabel (COMMA caseLabel)* COLON caseBranchStatements
    ;

caseLabel
    : expression (DOTDOT expression)?
    ;

forStatement
    : FOR expression ASSIGN expression TO expression (BY expression)? DO statementList END_FOR SEMI?
    ;

whileStatement
    : WHILE expression DO statementList END_WHILE SEMI?
    ;

repeatStatement
    : REPEAT statementList UNTIL expression SEMI? END_REPEAT SEMI?
    ;

tryStatement
    : TRY statementList
      (CATCH (LPAREN name RPAREN)? statementList)?
      (FINALLY statementList)?
      ENDTRY SEMI?
    ;

// ================================================================== expressions

expression
    : postfixExpression                                             # primaryExpr
    | <assoc = right> expression POWER expression                   # powerExpr
    | op = (MINUS | PLUS | NOT) expression                          # unaryExpr
    | expression op = (STAR | SLASH | MOD) expression               # multiplicativeExpr
    | expression op = (PLUS | MINUS) expression                     # additiveExpr
    | expression op = (LT | GT | LE | GE) expression                # relationalExpr
    | expression op = (EQ | NE) expression                          # equalityExpr
    | expression op = (AND | AND_THEN | AMPERSAND) expression       # andExpr
    | expression XOR expression                                     # xorExpr
    | expression op = (OR | OR_ELSE) expression                     # orExpr
    ;

postfixExpression
    : primary postfix*
    ;

postfix
    : DOT memberName                                   # memberAccess
    | LBRACKET expression (COMMA expression)* RBRACKET # indexAccess
    | CARET                                            # dereference
    | LPAREN argumentList? RPAREN                      # call
    ;

primary
    : literal
    | name HASH name                                   // typed enum value: E_State#Idle
    | name
    | THIS
    | SUPER
    | SYS_SYSTEM                                       // __SYSTEM.TYPE_CLASS.TYPE_INT
    | DIRECT_ADDRESS                                   // bOut := %IX0.1;
    | elementaryType                                  // TIME(), SIZEOF(INT), __NEW(INT, 10)
    | STRING
    | WSTRING
    | builtin
    | structInitializer
    | arrayInitializer
    | LPAREN expression RPAREN
    ;

builtin
    : NEW | DELETE | QUERYINTERFACE | QUERYPOINTER | ISVALIDREF
    ;

argumentList
    : argument (COMMA argument)* COMMA?                // TwinCAT accepts a trailing comma
    ;

argument
    : name ASSIGN expression?                          // input: nIn := 5 (TwinCAT accepts "nIn := ," too)
    | NOT? name OUTPUT_ASSIGN expression?              // output: bOut => bResult
    | expression                                       // positional
    ;

// (nA := 1, stB := (nC := 2)): struct values and FB initialisation
structInitializer
    : LPAREN structInitializerField (COMMA structInitializerField)* RPAREN
    ;

structInitializerField
    : name ASSIGN expression
    ;

// [1, 2, 3], [10(0)] (repetition parses as a call of the count)
arrayInitializer
    : LBRACKET expression (COMMA expression)* RBRACKET
    ;

literal
    : NUMBER
    | TYPED_LITERAL
    | DURATION_LITERAL
    | DATE_LITERAL
    | STRING_LITERAL
    | WSTRING_LITERAL
    | TRUE
    | FALSE
    ;

// ================================================================== names

name
    : IDENTIFIER
    ;

qualifiedName
    : (name | SYS_SYSTEM) (DOT memberName)*
    ;

// After '.' any word is a member: bit access (nWord.3), enum values and members named like keywords.
memberName
    : name
    | NUMBER
    | keyword
    | elementaryType
    | STRING
    | WSTRING
    ;

keyword
    : IF | THEN | ELSIF | ELSE | END_IF | CASE | OF | END_CASE | FOR | TO | BY | DO | END_FOR
    | WHILE | END_WHILE | REPEAT | UNTIL | END_REPEAT | EXIT | CONTINUE | RETURN | JMP
    | AND | OR | XOR | NOT | MOD | AND_THEN | OR_ELSE
    | VAR | VAR_INPUT | VAR_OUTPUT | VAR_IN_OUT | VAR_GLOBAL | VAR_TEMP | VAR_STAT | VAR_EXTERNAL | VAR_INST
    | VAR_CONFIG | END_VAR | CONSTANT | RETAIN | PERSISTENT | AT
    | PROGRAM | END_PROGRAM | FUNCTION | END_FUNCTION | FUNCTION_BLOCK | END_FUNCTION_BLOCK
    | METHOD | END_METHOD | PROPERTY | END_PROPERTY | INTERFACE | END_INTERFACE | ACTION | END_ACTION
    | EXTENDS | IMPLEMENTS | ABSTRACT | FINAL | PUBLIC | PRIVATE | PROTECTED | INTERNAL
    | TYPE | END_TYPE | STRUCT | END_STRUCT | UNION | END_UNION | ARRAY | POINTER | REFERENCE
    | TRUE | FALSE | THIS | SUPER | NEW | DELETE | QUERYINTERFACE | QUERYPOINTER | ISVALIDREF
    | TRY | CATCH | FINALLY | ENDTRY | SYS_SYSTEM
    ;
