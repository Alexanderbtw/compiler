using Compiler.Frontend.Translation.HIR.Expressions.Abstractions;
using Compiler.Frontend.Translation.HIR.Stringify;

namespace Compiler.Frontend.Translation.HIR.Expressions;

public sealed record StringHir(string Value, SourceSpan Span) : ExprHir(Span)
{
    public override string ToString() => $"\"" + HirPretty.Q(Value) + "\"";
}