using Compiler.Frontend.Translation.HIR.Expressions.Abstractions;
using Compiler.Frontend.Translation.HIR.Stringify;

namespace Compiler.Frontend.Translation.HIR.Expressions;

/// <summary>
///     Integer literal.
/// </summary>
public sealed record IntHir(
    long Value,
    SourceSpan Span) : ExprHir(Span)
{
    public override string ToString()
    {
        return Value.ToString();
    }
}
