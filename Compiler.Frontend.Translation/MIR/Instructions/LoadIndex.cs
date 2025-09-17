using Compiler.Frontend.Translation.MIR.Instructions.Abstractions;
using Compiler.Frontend.Translation.MIR.Operands;
using Compiler.Frontend.Translation.MIR.Operands.Abstractions;

namespace Compiler.Frontend.Translation.MIR.Instructions;

/// <summary>
///     Dst = Arr[Index] (array indexing).
/// </summary>
public sealed record LoadIndex(
    VReg Dst,
    MOperand Arr,
    MOperand Index) : MirInstr
{
    public override string ToString()
    {
        return $"{Dst} = loadidx {Arr}, {Index}";
    }
}
