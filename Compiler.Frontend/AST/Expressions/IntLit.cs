namespace Compiler.Frontend.AST.Expressions;

public sealed record IntLit(long Value) : Expr
{
    public override string ToString() => Value.ToString();
}
