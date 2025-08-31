using Compiler.Frontend.Translation.HIR.Expressions.Abstractions;
using Compiler.Frontend.Translation.HIR.Stringify;

namespace Compiler.Frontend.Translation.HIR.Expressions;

public sealed record CharHir(char Value, SourceSpan Span) : ExprHir(Span)
{
    public override string ToString() => $"'" + HirPretty.Qc(Value) + "'";
}