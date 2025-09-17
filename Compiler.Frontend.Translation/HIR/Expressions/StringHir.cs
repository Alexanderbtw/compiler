using Compiler.Frontend.Translation.HIR.Expressions.Abstractions;
using Compiler.Frontend.Translation.HIR.Stringify;

namespace Compiler.Frontend.Translation.HIR.Expressions;

/// <summary>
///     String literal.
/// </summary>
public sealed record StringHir(
    string Value,
    SourceSpan Span) : ExprHir(Span)
{
    public override string ToString()
    {
        return "\"" + HirPretty.Q(Value) + "\"";
    }
}
