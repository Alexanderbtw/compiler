namespace Compiler.Frontend.AST.Expressions;

public sealed record VarExpr(string Name) : Expr
{
    public override string ToString() => Name;
}
