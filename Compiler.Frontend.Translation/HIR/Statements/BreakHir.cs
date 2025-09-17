using Compiler.Frontend.Translation.HIR.Statements.Abstractions;
using Compiler.Frontend.Translation.HIR.Stringify;

namespace Compiler.Frontend.Translation.HIR.Statements;

/// <summary>
///     Break out of the nearest loop.
/// </summary>
public sealed record BreakHir(
    SourceSpan Span) : StmtHir(Span)
{
    public override string ToString()
    {
        return "break";
    }
}
