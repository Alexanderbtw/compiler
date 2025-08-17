using Compiler.Translation.HIR.Expressions.Abstractions;
using Compiler.Translation.HIR.Statements.Abstractions;
using Compiler.Translation.HIR.Stringify;

namespace Compiler.Translation.HIR.Expressions;

public sealed record IfHir(ExprHir Cond, StmtHir Then, StmtHir? Else, SourceSpan Span) : StmtHir(Span)
{
    public override string ToString() => Else is null
        ? $"if ({Cond}) {Then}"
        : $"if ({Cond}) {Then} else {Else}";
}