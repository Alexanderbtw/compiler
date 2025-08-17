using Compiler.Translation.HIR.Expressions.Abstractions;
using Compiler.Translation.HIR.Stringify;

namespace Compiler.Translation.HIR.Expressions;

public sealed record VarHir(string Name, SourceSpan Span) : ExprHir(Span)
{
    public override string ToString() => Name;
}