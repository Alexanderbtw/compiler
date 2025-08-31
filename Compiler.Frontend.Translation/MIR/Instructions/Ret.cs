using Compiler.Frontend.Translation.MIR.Instructions.Abstractions;
using Compiler.Frontend.Translation.MIR.Operands;

namespace Compiler.Frontend.Translation.MIR.Instructions;

public sealed record Ret(MOperand? Value) : MirInstr
{
    public override string ToString() => Value is null ? "ret" : $"ret {Value}";
}