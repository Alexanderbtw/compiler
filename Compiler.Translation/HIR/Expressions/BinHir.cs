using Compiler.Translation.HIR.Expressions.Abstractions;
using Compiler.Translation.HIR.Stringify;

namespace Compiler.Translation.HIR.Expressions;

public sealed record BinHir(BinOp Op, ExprHir Left, ExprHir Right, SourceSpan Span) : ExprHir(Span)
{
    public override string ToString() => $"({Left} {HirPretty.Op(Op)} {Right})";
}