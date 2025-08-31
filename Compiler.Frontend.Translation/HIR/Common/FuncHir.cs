using System.Collections.Generic;

using Compiler.Frontend.Translation.HIR.Statements;
using Compiler.Frontend.Translation.HIR.Stringify;

namespace Compiler.Frontend.Translation.HIR.Common;

public sealed record FuncHir(string Name, IReadOnlyList<string> Parameters, BlockHir Body, SourceSpan Span)
{
    public override string ToString() => $"func {Name}({HirPretty.Join(Parameters)}) {Body}";
}