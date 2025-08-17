using Compiler.Translation.HIR.Expressions.Abstractions;
using Compiler.Translation.HIR.Stringify;

namespace Compiler.Translation.HIR.Expressions;

public sealed record StringHir(string Value, SourceSpan Span) : ExprHir(Span)
{
    public override string ToString() => $"\"" + HirPretty.Q(Value) + "\"";
}