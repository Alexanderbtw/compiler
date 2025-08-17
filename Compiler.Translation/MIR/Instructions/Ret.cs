using Compiler.Translation.MIR.Instructions.Abstractions;
using Compiler.Translation.MIR.Operands;

namespace Compiler.Translation.MIR.Instructions;

public sealed record Ret(MOperand? Value) : MirInstr
{
    public override string ToString() => Value is null ? "ret" : $"ret {Value}";
}