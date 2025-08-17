using Compiler.Translation.MIR.Instructions.Abstractions;
using Compiler.Translation.MIR.Operands;

namespace Compiler.Translation.MIR.Instructions;

public sealed record Call(VReg? Dst, string Callee, IReadOnlyList<MOperand> Args) : MirInstr
{
    public override string ToString() => Dst is null
        ? $"call {Callee}({string.Join(", ", Args)})"
        : $"{Dst} = call {Callee}({string.Join(", ", Args)})";
}