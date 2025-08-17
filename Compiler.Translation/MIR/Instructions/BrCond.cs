using Compiler.Translation.MIR.Instructions.Abstractions;
using Compiler.Translation.MIR.Operands;

namespace Compiler.Translation.MIR.Instructions;

public sealed record BrCond(MOperand Cond, MirBlock IfTrue, MirBlock IfFalse) : MirInstr
{
    public override string ToString() => $"brcond {Cond}, %{IfTrue.Name}, %{IfFalse.Name}";
}