// ────────────────────────────────────────────────────────────────────────────────
// MiniLangParser.g4   –   syntax for the factorial / sort / sieve benchmark Lang
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
    | block                     // nested block
    ;

// variable declaration - e.g.  let i = 0;
variableDecl     : LET ID (ASSIGN expression)? SEMI ;

// control-flow
ifStmt           : IF LPAREN expression RPAREN statement (ELSE statement)? ;
whileStmt        : WHILE LPAREN expression RPAREN statement ;
forStmt          : FOR LPAREN
                       (variableDecl | exprList? SEMI)
                       expression?  SEMI
                       expressionList?
                   RPAREN statement ;

breakStmt        : BREAK    SEMI ;
continueStmt     : CONTINUE SEMI ;
returnStmt       : RETURN   expression? SEMI ;

// plain expression or an empty “;”
exprStmt         : expression? SEMI ;

// helpers
exprList         : expression (COMMA expression)* ;
expressionList   : expression (COMMA expression)* ;

// ────────────────
// 4.  Expressions  (Pratt / precedence-climbing style)
// ────────────────
expression       : assignment ;

assignment
    : postfixExpr ASSIGN assignment          #assign
    | logicalOr                              #simpleExpr
    ;

// logical ops
logicalOr        : logicalAnd (OR_OR logicalAnd)* ;
logicalAnd       : equality   (AND_AND equality)* ;

// ==  !=
equality         : comparison ((EQ | NEQ) comparison)* ;

// <  <=  >  >=
comparison       : addition   ((LT | LE | GT | GE) addition)* ;

// +  -
addition         : multiplication ((PLUS | MINUS) multiplication)* ;

// *  /  %
multiplication   : unary ((STAR | SLASH | PERCENT) unary)* ;

// unary +  -  !
unary            : (PLUS | MINUS | BANG) unary
                 | postfixExpr ;

// postfix: calls and indexing – left-recursive made safe by ANTLR precedence
postfixExpr  : primary ( callSuffix | indexSuffix )* ;

callSuffix   : LPAREN argumentList? RPAREN   ;  // #callSuffix (top-level)
indexSuffix  : LBRACK expression RBRACK      ;  // #indexSuffix

argumentList     : expression (COMMA expression)* ;

// atoms
primary
    : INT                         #intLiteral
    | STRING                      #stringLiteral
    | TRUE                        #boolTrue
    | FALSE                       #boolFalse
    | ID                          #identifier
    | LPAREN expression RPAREN    #parens
    ;