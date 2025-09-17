using Compiler.Frontend.Translation.MIR.Instructions.Abstractions;
using Compiler.Frontend.Translation.MIR.Operands;
using Compiler.Frontend.Translation.MIR.Operands.Abstractions;

namespace Compiler.Frontend.Translation.MIR.Instructions;

/// <summary>
///     Function return (optional value).
/// </summary>
public sealed record Ret(
    MOperand? Value) : MirInstr
{
    public override string ToString()
    {
        return Value is null
            ? "ret"
            : $"ret {Value}";
    }
}
