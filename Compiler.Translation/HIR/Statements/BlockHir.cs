using System.Collections.Generic;
using System.Linq;

using Compiler.Translation.HIR.Statements.Abstractions;
using Compiler.Translation.HIR.Stringify;

namespace Compiler.Translation.HIR.Statements;

public sealed record BlockHir(IReadOnlyList<StmtHir> Statements, SourceSpan Span) : StmtHir(Span)
{
    public override string ToString() => "{ " + string.Join("; ", Statements.Select(s => s.ToString())) + " }";
}