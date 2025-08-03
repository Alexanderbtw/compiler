namespace Compiler.Frontend.AST.Expressions;

public sealed record StringLit(string Value) : Expr
{
    public override string ToString() => $"\"{Value}\"";
}
