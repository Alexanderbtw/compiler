namespace Compiler.Translation.MIR.Operands;

public sealed record Const(object? Value) : MOperand
{
    public override string ToString() => Value is string s ? $"\"{s}\"" : Value?.ToString() ?? "null";
}