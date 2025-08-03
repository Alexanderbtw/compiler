namespace Compiler.Frontend.AST.Expressions;

public sealed record CharLit(char Value) : Expr
{
    public override string ToString() => $"'{Value}'";
}
