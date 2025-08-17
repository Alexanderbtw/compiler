using Compiler.Translation.MIR.Instructions.Abstractions;
using Compiler.Translation.MIR.Operands;

namespace Compiler.Translation.MIR.Instructions;

public sealed record Bin(VReg Dst, MBinOp Op, MOperand L, MOperand R) : MirInstr
{
    public override string ToString() => $"{Dst} = {Op.ToString().ToLower()} {L}, {R}";
}