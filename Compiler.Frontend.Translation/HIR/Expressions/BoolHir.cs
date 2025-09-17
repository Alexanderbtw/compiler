using Compiler.Frontend.Translation.HIR.Expressions.Abstractions;
using Compiler.Frontend.Translation.HIR.Stringify;

namespace Compiler.Frontend.Translation.HIR.Expressions;

/// <summary>
///     Boolean literal.
/// </summary>
public sealed record BoolHir(
    bool Value,
    SourceSpan Span) : ExprHir(Span)
{
    public override string ToString()
    {
        return Value
            ? "true"
            : "false";
    }
}
