using Compiler.Frontend.Translation.HIR.Expressions.Abstractions;
using Compiler.Frontend.Translation.HIR.Stringify;

namespace Compiler.Frontend.Translation.HIR.Expressions;

/// <summary>
///     Binary expression.
/// </summary>
public sealed record BinHir(
    BinOp Op,
    ExprHir Left,
    ExprHir Right,
    SourceSpan Span) : ExprHir(Span)
{
    public override string ToString()
    {
        return $"({Left} {HirPretty.Op(Op)} {Right})";
    }
}
