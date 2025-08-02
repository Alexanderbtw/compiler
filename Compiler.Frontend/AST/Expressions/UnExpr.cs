namespace Compiler.Frontend.AST.Expressions;

public sealed record UnExpr(string Op, Expr R) : Expr;
