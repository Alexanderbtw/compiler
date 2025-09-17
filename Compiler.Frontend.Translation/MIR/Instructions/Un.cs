using Compiler.Frontend.Translation.MIR.Instructions.Abstractions;
using Compiler.Frontend.Translation.MIR.Operands;
using Compiler.Frontend.Translation.MIR.Operands.Abstractions;

namespace Compiler.Frontend.Translation.MIR.Instructions;

/// <summary>
///     Unary op: Dst = Op X.
/// </summary>
public sealed record Un(
    VReg Dst,
    MUnOp Op,
    MOperand X) : MirInstr
{
    public override string ToString()
    {
        return $"{Dst} = {Op.ToString().ToLower()} {X}";
    }
}
