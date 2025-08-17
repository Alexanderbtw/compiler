using Compiler.Translation.HIR.Expressions.Abstractions;
using Compiler.Translation.HIR.Statements.Abstractions;
using Compiler.Translation.HIR.Stringify;

namespace Compiler.Translation.HIR.Expressions;

public sealed record ExprStmtHir(ExprHir? Expr, SourceSpan Span) : StmtHir(Span)
{
    public override string ToString() => Expr is null ? ";" : $"{Expr};";
}