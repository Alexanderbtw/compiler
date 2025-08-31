using System.Collections.Generic;
using System.Linq;

using Compiler.Frontend.Translation.HIR.Statements.Abstractions;
using Compiler.Frontend.Translation.HIR.Stringify;

namespace Compiler.Frontend.Translation.HIR.Statements;

public sealed record BlockHir(IReadOnlyList<StmtHir> Statements, SourceSpan Span) : StmtHir(Span)
{
    public override string ToString() => "{ " + string.Join("; ", Statements.Select(s => s.ToString())) + " }";
}