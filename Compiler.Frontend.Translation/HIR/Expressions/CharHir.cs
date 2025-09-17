using Compiler.Frontend.Translation.HIR.Expressions.Abstractions;
using Compiler.Frontend.Translation.HIR.Stringify;

namespace Compiler.Frontend.Translation.HIR.Expressions;

/// <summary>
///     Char literal.
/// </summary>
public sealed record CharHir(
    char Value,
    SourceSpan Span) : ExprHir(Span)
{
    public override string ToString()
    {
        return "'" + HirPretty.Qc(Value) + "'";
    }
}
