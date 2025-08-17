using Compiler.Translation.MIR.Instructions.Abstractions;
using Compiler.Translation.MIR.Operands;

namespace Compiler.Translation.MIR.Instructions;

public sealed record Un(VReg Dst, MUnOp Op, MOperand X) : MirInstr
{
    public override string ToString() => $"{Dst} = {Op.ToString().ToLower()} {X}";
}