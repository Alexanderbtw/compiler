using Compiler.Frontend.Translation.MIR.Instructions.Abstractions;
using Compiler.Frontend.Translation.MIR.Operands;
using Compiler.Frontend.Translation.MIR.Operands.Abstractions;

namespace Compiler.Frontend.Translation.MIR.Instructions;

/// <summary>
///     Binary op: Dst = Op L, R.
/// </summary>
public sealed record Bin(
    VReg Dst,
    MBinOp Op,
    MOperand L,
    MOperand R) : MirInstr
{
    public override string ToString()
    {
        return $"{Dst} = {Op.ToString().ToLower()} {L}, {R}";
    }
}
