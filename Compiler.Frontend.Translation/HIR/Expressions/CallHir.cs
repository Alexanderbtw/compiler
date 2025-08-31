using System.Collections.Generic;

using Compiler.Frontend.Translation.HIR.Expressions.Abstractions;
using Compiler.Frontend.Translation.HIR.Stringify;

namespace Compiler.Frontend.Translation.HIR.Expressions;

public sealed record CallHir(ExprHir Callee, IReadOnlyList<ExprHir> Args, SourceSpan Span) : ExprHir(Span)
{
    public override string ToString() => $"{Callee}({HirPretty.Join(Args)})";
}