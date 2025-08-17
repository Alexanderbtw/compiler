using System.Collections.Generic;

using Compiler.Translation.HIR.Statements;
using Compiler.Translation.HIR.Stringify;

namespace Compiler.Translation.HIR.Common;

public sealed record FuncHir(string Name, IReadOnlyList<string> Parameters, BlockHir Body, SourceSpan Span)
{
    public override string ToString() => $"func {Name}({HirPretty.Join(Parameters)}) {Body}";
}