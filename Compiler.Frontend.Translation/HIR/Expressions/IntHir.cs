using Compiler.Frontend.Translation.HIR.Expressions.Abstractions;
using Compiler.Frontend.Translation.HIR.Stringify;

namespace Compiler.Frontend.Translation.HIR.Expressions;

public sealed record IntHir(long Value, SourceSpan Span) : ExprHir(Span)
{
    public override string ToString() => Value.ToString();
}