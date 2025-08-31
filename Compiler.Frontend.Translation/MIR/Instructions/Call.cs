using Compiler.Frontend.Translation.MIR.Instructions.Abstractions;
using Compiler.Frontend.Translation.MIR.Operands;

namespace Compiler.Frontend.Translation.MIR.Instructions;

public sealed record Call(VReg? Dst, string Callee, IReadOnlyList<MOperand> Args) : MirInstr
{
    public override string ToString() => Dst is null
        ? $"call {Callee}({string.Join(", ", Args)})"
        : $"{Dst} = call {Callee}({string.Join(", ", Args)})";
}