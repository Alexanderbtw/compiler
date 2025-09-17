using Compiler.Frontend.Translation.HIR.Expressions.Abstractions;
using Compiler.Frontend.Translation.HIR.Stringify;

namespace Compiler.Frontend.Translation.HIR.Expressions;

/// <summary>
///     Variable reference.
/// </summary>
public sealed record VarHir(
    string Name,
    SourceSpan Span) : ExprHir(Span)
{
    public override string ToString()
    {
        return Name;
    }
}
