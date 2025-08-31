using Compiler.Frontend.Translation.MIR.Instructions.Abstractions;
using Compiler.Frontend.Translation.MIR.Operands;

namespace Compiler.Frontend.Translation.MIR.Instructions;

public sealed record Un(VReg Dst, MUnOp Op, MOperand X) : MirInstr
{
    public override string ToString() => $"{Dst} = {Op.ToString().ToLower()} {X}";
}