using Compiler.Translation.MIR.Instructions.Abstractions;

namespace Compiler.Translation.MIR.Instructions;

public sealed record Br(MirBlock Target) : MirInstr
{
    public override string ToString() => $"br %{Target.Name}";
}