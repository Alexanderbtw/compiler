using Compiler.Translation.HIR.Expressions.Abstractions;
using Compiler.Translation.HIR.Stringify;

namespace Compiler.Translation.HIR.Expressions;

public sealed record IntHir(long Value, SourceSpan Span) : ExprHir(Span)
{
    public override string ToString() => Value.ToString();
}