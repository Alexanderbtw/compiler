using Compiler.Translation.HIR.Statements.Abstractions;
using Compiler.Translation.HIR.Stringify;

namespace Compiler.Translation.HIR.Statements;

public sealed record BreakHir(SourceSpan Span) : StmtHir(Span)
{
    public override string ToString() => "break";
}