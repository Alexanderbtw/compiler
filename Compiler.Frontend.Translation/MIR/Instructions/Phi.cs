using Compiler.Frontend.Translation.MIR.Instructions.Abstractions;
using Compiler.Frontend.Translation.MIR.Operands;
using Compiler.Frontend.Translation.MIR.Operands.Abstractions;

namespace Compiler.Frontend.Translation.MIR.Instructions;

/// <summary>
///     SSA phi node: select a value based on predecessor.
///     (Currently unused by the backends; here for completeness.)
/// </summary>
public sealed record Phi(
    VReg Dst,
    IReadOnlyList<(MirBlock block, MOperand value)> Incomings) : MirInstr
{
    public override string ToString()
    {
        return $"{Dst} = phi " + string.Join(
            separator: ", ",
            values: Incomings.Select(i => $"[{i.block.Name}: {i.value}]"));
    }
}
