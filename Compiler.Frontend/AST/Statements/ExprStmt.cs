using Compiler.Frontend.AST.Expressions;

namespace Compiler.Frontend.AST.Statements;

public sealed record ExprStmt(Expr? E) : Stmt
{
    public override string ToString() => E is null ? ";" : $"{E};";
}
