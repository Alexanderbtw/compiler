using Compiler.Frontend.Translation.HIR.Statements;
using Compiler.Frontend.Translation.HIR.Stringify;

namespace Compiler.Frontend.Translation.HIR.Common;

/// <summary>
///     A function in HIR with a name, parameters and a block body.
/// </summary>
public sealed record FuncHir(
    string Name,
    IReadOnlyList<string> Parameters,
    BlockHir Body,
    SourceSpan Span)
{
    public override string ToString()
    {
        return $"func {Name}({HirPretty.Join(Parameters)}) {Body}";
    }
}
