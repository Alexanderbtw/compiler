using System.Collections.Generic;

using Compiler.Translation.HIR.Expressions.Abstractions;
using Compiler.Translation.HIR.Stringify;

namespace Compiler.Translation.HIR.Expressions;

public sealed record CallHir(ExprHir Callee, IReadOnlyList<ExprHir> Args, SourceSpan Span) : ExprHir(Span)
{
    public override string ToString() => $"{Callee}({HirPretty.Join(Args)})";
}