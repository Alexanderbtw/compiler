using System.Collections.Generic;

using Compiler.Frontend.Translation.HIR.Stringify;

namespace Compiler.Frontend.Translation.HIR.Common;

public sealed record ProgramHir(IReadOnlyList<FuncHir> Functions)
{
    public override string ToString() => $"Program({HirPretty.Join(Functions)})";
}