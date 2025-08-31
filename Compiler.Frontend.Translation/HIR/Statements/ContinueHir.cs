using Compiler.Frontend.Translation.HIR.Statements.Abstractions;
using Compiler.Frontend.Translation.HIR.Stringify;

namespace Compiler.Frontend.Translation.HIR.Statements;

public sealed record ContinueHir(
    SourceSpan Span) : StmtHir(Span)
{
    public override string ToString()
    {
        return "continue";
    }
}
