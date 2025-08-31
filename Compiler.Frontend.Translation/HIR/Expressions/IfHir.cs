using Compiler.Frontend.Translation.HIR.Expressions.Abstractions;
using Compiler.Frontend.Translation.HIR.Statements.Abstractions;
using Compiler.Frontend.Translation.HIR.Stringify;

namespace Compiler.Frontend.Translation.HIR.Expressions;

public sealed record IfHir(
    ExprHir Cond,
    StmtHir Then,
    StmtHir? Else,
    SourceSpan Span) : StmtHir(Span)
{
    public override string ToString()
    {
        return Else is null
            ? $"if ({Cond}) {Then}"
            : $"if ({Cond}) {Then} else {Else}";
    }
}
