using Compiler.Translation.HIR.Expressions.Abstractions;
using Compiler.Translation.HIR.Stringify;

namespace Compiler.Translation.HIR.Expressions;

public sealed record CharHir(char Value, SourceSpan Span) : ExprHir(Span)
{
    public override string ToString() => $"'" + HirPretty.Qc(Value) + "'";
}