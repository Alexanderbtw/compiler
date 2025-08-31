using Compiler.Frontend.Translation.MIR.Instructions.Abstractions;
using Compiler.Frontend.Translation.MIR.Operands;

namespace Compiler.Frontend.Translation.MIR.Instructions;

public sealed record Move(
    VReg Dst,
    MOperand Src) : MirInstr
{
    public override string ToString()
    {
        return $"{Dst} = {Src}";
    }
}
