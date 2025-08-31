using Compiler.Frontend.Translation.MIR.Instructions.Abstractions;
using Compiler.Frontend.Translation.MIR.Operands;

namespace Compiler.Frontend.Translation.MIR.Instructions;

public sealed record Bin(VReg Dst, MBinOp Op, MOperand L, MOperand R) : MirInstr
{
    public override string ToString() => $"{Dst} = {Op.ToString().ToLower()} {L}, {R}";
}