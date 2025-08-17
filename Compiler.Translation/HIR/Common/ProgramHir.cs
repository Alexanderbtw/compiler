using System.Collections.Generic;

using Compiler.Translation.HIR.Stringify;

namespace Compiler.Translation.HIR.Common;

public sealed record ProgramHir(IReadOnlyList<FuncHir> Functions)
{
    public override string ToString() => $"Program({HirPretty.Join(Functions)})";
}