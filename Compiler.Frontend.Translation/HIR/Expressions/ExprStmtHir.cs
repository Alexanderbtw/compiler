using Compiler.Frontend.Translation.HIR.Expressions.Abstractions;
using Compiler.Frontend.Translation.HIR.Statements.Abstractions;
using Compiler.Frontend.Translation.HIR.Stringify;

namespace Compiler.Frontend.Translation.HIR.Expressions;

/// <summary>
///     Expression statement (discard result or void call).
/// </summary>
public sealed record ExprStmtHir(
    ExprHir? Expr,
    SourceSpan Span) : StmtHir(Span)
{
    public override string ToString()
    {
        return Expr is null
            ? ";"
            : $"{Expr};";
    }
}
