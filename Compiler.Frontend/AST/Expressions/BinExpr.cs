namespace Compiler.Frontend.AST.Expressions;

public sealed record BinExpr(string Op, Expr L, Expr R) : Expr
{
    public override string ToString() => $"({L} {Op} {R})";
}
