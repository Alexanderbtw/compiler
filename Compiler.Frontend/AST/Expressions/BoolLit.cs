namespace Compiler.Frontend.AST.Expressions;

public sealed record BoolLit(bool Value) : Expr
{
    public override string ToString() => Value.ToString().ToLower();
}
