using Compiler.Frontend.Translation.MIR.Instructions.Abstractions;
using Compiler.Frontend.Translation.MIR.Operands;

namespace Compiler.Frontend.Translation.MIR.Instructions;

public sealed record StoreIndex(
    MOperand Arr,
    MOperand Index,
    MOperand Value) : MirInstr
{
    public override string ToString()
    {
        return $"storeidx {Arr}, {Index}, {Value}";
    }
}
