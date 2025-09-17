using Compiler.Frontend.Translation.HIR.Expressions.Abstractions;
using Compiler.Frontend.Translation.HIR.Statements.Abstractions;
using Compiler.Frontend.Translation.HIR.Stringify;

namespace Compiler.Frontend.Translation.HIR.Expressions;

/// <summary>
///     While loop.
/// </summary>
public sealed record WhileHir(
    ExprHir Cond,
    StmtHir Body,
    SourceSpan Span) : StmtHir(Span)
{
    public override string ToString()
    {
        return $"while ({Cond}) {Body}";
    }
}
