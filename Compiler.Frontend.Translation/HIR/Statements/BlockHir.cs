using Compiler.Frontend.Translation.HIR.Statements.Abstractions;
using Compiler.Frontend.Translation.HIR.Stringify;

namespace Compiler.Frontend.Translation.HIR.Statements;

/// <summary>
///     A block of statements.
/// </summary>
public sealed record BlockHir(
    IReadOnlyList<StmtHir> Statements,
    SourceSpan Span) : StmtHir(Span)
{
    public override string ToString()
    {
        return "{ " + string.Join(
            separator: "; ",
            values: Statements.Select(s => s.ToString())) + " }";
    }
}
