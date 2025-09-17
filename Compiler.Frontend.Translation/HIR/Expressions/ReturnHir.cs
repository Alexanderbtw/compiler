using Compiler.Frontend.Translation.HIR.Expressions.Abstractions;
using Compiler.Frontend.Translation.HIR.Statements.Abstractions;
using Compiler.Frontend.Translation.HIR.Stringify;

namespace Compiler.Frontend.Translation.HIR.Expressions;

/// <summary>
///     Return from the current function with an optional value.
/// </summary>
public sealed record ReturnHir(
    ExprHir? Expr,
    SourceSpan Span) : StmtHir(Span)
{
    public override string ToString()
    {
        return Expr is null
            ? "return"
            : $"return {Expr}";
    }
}
