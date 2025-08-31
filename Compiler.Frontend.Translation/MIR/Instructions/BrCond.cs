using Compiler.Frontend.Translation.MIR.Instructions.Abstractions;
using Compiler.Frontend.Translation.MIR.Operands;

namespace Compiler.Frontend.Translation.MIR.Instructions;

public sealed record BrCond(MOperand Cond, MirBlock IfTrue, MirBlock IfFalse) : MirInstr
{
    public override string ToString() => $"brcond {Cond}, %{IfTrue.Name}, %{IfFalse.Name}";
}