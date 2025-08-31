using Compiler.Frontend.Translation.HIR.Expressions.Abstractions;
using Compiler.Frontend.Translation.HIR.Stringify;

namespace Compiler.Frontend.Translation.HIR.Expressions;

public sealed record UnHir(UnOp Op, ExprHir Operand, SourceSpan Span) : ExprHir(Span)
{
    public override string ToString() => $"({HirPretty.Op(Op)}{Operand})";
}