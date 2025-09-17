using Compiler.Frontend.Translation.MIR.Instructions.Abstractions;
using Compiler.Frontend.Translation.MIR.Operands;
using Compiler.Frontend.Translation.MIR.Operands.Abstractions;

namespace Compiler.Frontend.Translation.MIR.Instructions;

/// <summary>
///     Arr[Index] = Value (array store).
/// </summary>
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
