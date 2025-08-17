using Compiler.Translation.MIR.Instructions.Abstractions;
using Compiler.Translation.MIR.Operands;

namespace Compiler.Translation.MIR.Instructions;

public sealed record LoadIndex(VReg Dst, MOperand Arr, MOperand Index) : MirInstr
{
    public override string ToString() => $"{Dst} = loadidx {Arr}, {Index}";
}