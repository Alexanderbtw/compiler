// ────────────────────────────────────────────────────────────────────────────────
// MiniLangParser.g4  –  minimal syntax for the factorial / sort / sieve language
// ────────────────────────────────────────────────────────────────────────────────
parser grammar MiniLangParser;

options { tokenVocab = MiniLangLexer; language = CSharp; }

// ────────────────
// 1.  Top level
// ────────────────
program          : functionDecl* EOF ;

// ────────────────
// 2.  Declarations
// ────────────────
functionDecl     : FN ID LPAREN paramList? RPAREN block ;
paramList        : ID (COMMA ID)* ;

// ────────────────
// 3.  Blocks & statements
// ────────────────
block            : LBRACE statement* RBRACE ;

statement
    : variableDecl
    | ifStmt
    | whileStmt
    | forStmt
    | breakStmt
    | continueStmt
    | returnStmt
    | exprStmt
    | block
    ;

// variable declaration – e.g.  var i = 0;
variableDecl     : VAR ID (ASSIGN expression)? SEMI ;

// control-flow
ifStmt           : IF LPAREN expression RPAREN statement (ELSE statement)? ;
whileStmt        : WHILE LPAREN expression RPAREN statement ;
forStmt          : FOR LPAREN
                      (variableDecl | expressionList? SEMI)   // init
                      expression? SEMI                        // condition
                      expressionList?                         // iterator
                  RPAREN statement ;

breakStmt        : BREAK    SEMI ;
continueStmt     : CONTINUE SEMI ;
returnStmt       : RETURN   expression? SEMI ;

// plain expression or an empty ‘;’
exprStmt         : expression? SEMI ;

// helpers
expressionList   : expression (COMMA expression)* ;

// ────────────────
// 4.  Expressions (operator precedence)
// ────────────────
expression       : assignment ;

assignment
    : postfixExpr ASSIGN assignment          #assign
    | logicalOr                              #simpleExpr
    ;

// logical ops
logicalOr        : logicalAnd (OR_OR logicalAnd)* ;
logicalAnd       : equality   (AND_AND equality)* ;

// == !=
equality         : comparison ((EQ | NEQ) comparison)* ;

// < <= > >=
comparison       : addition   ((LT | LE | GT | GE) addition)* ;

// + -
addition         : multiplication ((PLUS | MINUS) multiplication)* ;

// * / %
multiplication   : unary ((STAR | SLASH | PERCENT) unary)* ;

// unary + - !
unary            : (PLUS | MINUS | BANG) unary
                 | postfixExpr ;

// postfix: calls and indexing
postfixExpr      : primary (callSuffix | indexSuffix)* ;
callSuffix       : LPAREN argumentList? RPAREN ;
indexSuffix      : LBRACK expression RBRACK ;

argumentList     : expression (COMMA expression)* ;

// ────────────────
// 5.  Primary atoms
// ────────────────
primary
    : INT                         #intLiteral
    | STRING                      #stringLiteral
    | CHAR                        #charLiteral
    | TRUE                        #boolTrue
    | FALSE                       #boolFalse
    | ID                          #identifier
    | LPAREN expression RPAREN    #parens
    ;