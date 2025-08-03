namespace Compiler.Frontend.AST.Statements;

public sealed record Break : Stmt
{
    public override string ToString() => "break;";
}