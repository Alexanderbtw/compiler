namespace Compiler.Frontend.AST.Statements;

public sealed record Continue : Stmt
{
    public override string ToString() => "continue;";
}
