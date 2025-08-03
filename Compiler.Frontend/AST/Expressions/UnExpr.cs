namespace Compiler.Frontend.AST.Expressions;

public sealed record UnExpr(string Op, Expr R) : Expr
{
    public override string ToString() => $"({Op}{R})";
}
