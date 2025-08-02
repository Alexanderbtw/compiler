namespace Compiler.Frontend.AST.Expressions;

public sealed record IndexExpr(Expr Arr, Expr Index) : Expr;
