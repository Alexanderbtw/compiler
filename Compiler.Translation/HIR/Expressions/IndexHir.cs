using Compiler.Translation.HIR.Expressions.Abstractions;
using Compiler.Translation.HIR.Stringify;

namespace Compiler.Translation.HIR.Expressions;

public sealed record IndexHir(ExprHir Target, ExprHir Index, SourceSpan Span) : ExprHir(Span)
{
    public override string ToString() => $"{Target}[{Index}]";
}