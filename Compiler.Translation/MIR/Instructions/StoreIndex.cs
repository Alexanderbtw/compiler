using Compiler.Translation.MIR.Instructions.Abstractions;
using Compiler.Translation.MIR.Operands;

namespace Compiler.Translation.MIR.Instructions;

public sealed record StoreIndex(MOperand Arr, MOperand Index, MOperand Value) : MirInstr
{
    public override string ToString() => $"storeidx {Arr}, {Index}, {Value}";
}