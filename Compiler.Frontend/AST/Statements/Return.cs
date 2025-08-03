using Compiler.Frontend.AST.Expressions;

namespace Compiler.Frontend.AST.Statements;

public sealed record Return(Expr? Value) : Stmt
{
    public override string ToString() =>
        Value is null ? "return;" : $"return {Value};";
}
