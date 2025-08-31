using Compiler.Frontend.Translation.MIR.Instructions.Abstractions;
using Compiler.Frontend.Translation.MIR.Operands;

namespace Compiler.Frontend.Translation.MIR.Instructions;

public sealed record Phi(VReg Dst, IReadOnlyList<(MirBlock block, MOperand value)> Incomings) : MirInstr
{
    public override string ToString() => $"{Dst} = phi " + string.Join(
        ", ",
        Incomings.Select(i => $"[{i.block.Name}: {i.value}]"));
}