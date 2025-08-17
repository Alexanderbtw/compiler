lexer grammar MiniLangLexer;

// ────────────────
// 1.  Channels & basic options
// ────────────────
channels { COMMENTS }                 // we’ll hide comments on a side channel
options  { language = CSharp; }       // generate C# lexer

// ────────────────
// 2.  Whitespace & comments   (skipped → never reach the parser)
// ────────────────
LINE_COMMENT : '//' ~[\r\n]* -> channel(COMMENTS);     // single-line comments
BLOCK_COMMENT: '/*' .*? '*/'   -> channel(COMMENTS);   // /* … */
WS           : [ \t\r\n]+       -> skip;               // any whitespace

// ────────────────
// 3.  Keywords   (exact spelling, no escapes, no unicode identifiers)
// ────────────────
FN       : 'fn';
RETURN   : 'return';
VAR      : 'var';
IF       : 'if';
ELSE     : 'else';
WHILE    : 'while';
FOR      : 'for';
BREAK    : 'break';
CONTINUE : 'continue';
TRUE     : 'true';
FALSE    : 'false';

// ────────────────
// 4.  Operators & punctuation   (only what the grammar actually uses)
// ────────────────
LBRACE  : '{';
RBRACE  : '}';
LPAREN  : '(';
RPAREN  : ')';
LBRACK  : '[';
RBRACK  : ']';
COMMA   : ',';
SEMI    : ';';
COLON   : ':';
DOT     : '.';

PLUS     : '+';
MINUS    : '-';
STAR     : '*';
SLASH    : '/';
PERCENT  : '%';

ASSIGN   : '=';
EQ       : '==';
NEQ      : '!=';
LT       : '<';
GT       : '>';
LE       : '<=';
GE       : '>=';

AND_AND  : '&&';
OR_OR    : '||';
BANG     : '!';

// ────────────────
// 5.  Literals
// ────────────────
INT      : [0-9]+;                                 // 20! still fits in 64-bit
// (add REAL if you later introduce floats)

STRING   : '"' ( '\\' . | ~["\\\r\n])* '"' ;       // optional, e.g. debug prints
CHAR     : '\'' ( '\\' . | ~['\\\r\n] ) '\'' ;     // rarely needed, but tiny rule

// ────────────────
// 6.  Identifiers
// ────────────────
ID       : [a-zA-Z_][a-zA-Z_0-9]*;                 // ASCII only – keeps lexer tiny

// ────────────────
// 7.  Error catch-all
// ────────────────
ERROR_CHAR : . ;                                   // any stray character ⇒ error