using Compiler.Frontend.Translation.MIR.Instructions.Abstractions;

namespace Compiler.Frontend.Translation.MIR.Instructions;

public sealed record Br(
    MirBlock Target) : MirInstr
{
    public override string ToString()
    {
        return $"br %{Target.Name}";
    }
}
