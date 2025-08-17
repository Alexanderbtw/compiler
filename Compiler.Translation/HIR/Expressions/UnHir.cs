using Compiler.Translation.HIR.Expressions.Abstractions;
using Compiler.Translation.HIR.Stringify;

namespace Compiler.Translation.HIR.Expressions;

public sealed record UnHir(UnOp Op, ExprHir Operand, SourceSpan Span) : ExprHir(Span)
{
    public override string ToString() => $"({HirPretty.Op(Op)}{Operand})";
}