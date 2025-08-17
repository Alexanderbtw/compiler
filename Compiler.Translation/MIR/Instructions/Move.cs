using Compiler.Translation.MIR.Instructions.Abstractions;
using Compiler.Translation.MIR.Operands;

namespace Compiler.Translation.MIR.Instructions;

public sealed record Move(VReg Dst, MOperand Src) : MirInstr
{
    public override string ToString() => $"{Dst} = {Src}";
}