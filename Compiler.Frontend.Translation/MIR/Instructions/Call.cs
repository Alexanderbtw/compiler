using Compiler.Frontend.Translation.MIR.Instructions.Abstractions;
using Compiler.Frontend.Translation.MIR.Operands;
using Compiler.Frontend.Translation.MIR.Operands.Abstractions;

namespace Compiler.Frontend.Translation.MIR.Instructions;

/// <summary>
///     Call a function by name. Dst is optional (for procedures).
/// </summary>
public sealed record Call(
    VReg? Dst,
    string Callee,
    IReadOnlyList<MOperand> Args) : MirInstr
{
    public override string ToString()
    {
        return Dst is null
            ? $"call {Callee}({string.Join(separator: ", ", values: Args)})"
            : $"{Dst} = call {Callee}({string.Join(separator: ", ", values: Args)})";
    }
}
