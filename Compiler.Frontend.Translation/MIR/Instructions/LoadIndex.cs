using Compiler.Frontend.Translation.MIR.Instructions.Abstractions;
using Compiler.Frontend.Translation.MIR.Operands;

namespace Compiler.Frontend.Translation.MIR.Instructions;

public sealed record LoadIndex(VReg Dst, MOperand Arr, MOperand Index) : MirInstr
{
    public override string ToString() => $"{Dst} = loadidx {Arr}, {Index}";
}