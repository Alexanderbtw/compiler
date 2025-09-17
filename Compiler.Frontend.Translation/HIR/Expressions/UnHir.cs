using Compiler.Frontend.Translation.HIR.Expressions.Abstractions;
using Compiler.Frontend.Translation.HIR.Stringify;

namespace Compiler.Frontend.Translation.HIR.Expressions;

/// <summary>
///     Unary expression.
/// </summary>
public sealed record UnHir(
    UnOp Op,
    ExprHir Operand,
    SourceSpan Span) : ExprHir(Span)
{
    public override string ToString()
    {
        return $"({HirPretty.Op(Op)}{Operand})";
    }
}
