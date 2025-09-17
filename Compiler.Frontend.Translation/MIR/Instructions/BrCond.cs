using Compiler.Frontend.Translation.MIR.Instructions.Abstractions;
using Compiler.Frontend.Translation.MIR.Operands;
using Compiler.Frontend.Translation.MIR.Operands.Abstractions;

namespace Compiler.Frontend.Translation.MIR.Instructions;

/// <summary>
///     Conditional branch: if (Cond) goto IfTrue else goto IfFalse.
/// </summary>
public sealed record BrCond(
    MOperand Cond,
    MirBlock IfTrue,
    MirBlock IfFalse) : MirInstr
{
    public override string ToString()
    {
        return $"brcond {Cond}, %{IfTrue.Name}, %{IfFalse.Name}";
    }
}
