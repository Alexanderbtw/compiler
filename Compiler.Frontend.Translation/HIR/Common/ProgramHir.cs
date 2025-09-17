using Compiler.Frontend.Translation.HIR.Stringify;

namespace Compiler.Frontend.Translation.HIR.Common;

/// <summary>
///     A program is just a list of functions in HIR form.
/// </summary>
public sealed record ProgramHir(
    IReadOnlyList<FuncHir> Functions)
{
    public override string ToString()
    {
        return $"Program({HirPretty.Join(Functions)})";
    }
}
