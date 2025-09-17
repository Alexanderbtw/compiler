using Compiler.Frontend.Translation.MIR.Operands.Abstractions;

namespace Compiler.Frontend.Translation.MIR.Operands;

/// <summary>
///     SSA-like virtual register identifier
/// </summary>
public sealed record VReg(
    int Id) : MOperand
{
    public override string ToString()
    {
        return $"%t{Id}";
    }
}
