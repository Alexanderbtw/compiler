using Compiler.Frontend.Translation.MIR.Instructions.Abstractions;
using Compiler.Frontend.Translation.MIR.Operands;
using Compiler.Frontend.Translation.MIR.Operands.Abstractions;

namespace Compiler.Frontend.Translation.MIR.Instructions;

/// <summary>
///     Copy/move: Dst = Src. Often produced by folding.
/// </summary>
public sealed record Move(
    VReg Dst,
    MOperand Src) : MirInstr
{
    public override string ToString()
    {
        return $"{Dst} = {Src}";
    }
}
