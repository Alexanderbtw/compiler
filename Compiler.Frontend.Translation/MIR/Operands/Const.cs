using Compiler.Frontend.Translation.MIR.Operands.Abstractions;

namespace Compiler.Frontend.Translation.MIR.Operands;

public sealed record Const(
    object? Value) : MOperand
{
    public override string ToString()
    {
        return Value is string s
            ? $"\"{s}\""
            : Value?.ToString() ?? "null";
    }
}
