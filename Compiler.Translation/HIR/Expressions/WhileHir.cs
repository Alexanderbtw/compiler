using Compiler.Translation.HIR.Expressions.Abstractions;
using Compiler.Translation.HIR.Statements.Abstractions;
using Compiler.Translation.HIR.Stringify;

namespace Compiler.Translation.HIR.Expressions;

public sealed record WhileHir(ExprHir Cond, StmtHir Body, SourceSpan Span) : StmtHir(Span)
{
    public override string ToString() => $"while ({Cond}) {Body}";
}