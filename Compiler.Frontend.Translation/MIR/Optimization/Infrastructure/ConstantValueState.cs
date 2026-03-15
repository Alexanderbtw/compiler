namespace Compiler.Frontend.Translation.MIR.Optimization.Infrastructure;

public enum ConstantValueKind
{
    Unknown = 0,
    Constant = 1,
    Overdefined = 2
}

public readonly record struct ConstantValueState(
    ConstantValueKind Kind,
    object? Value)
{
    public static ConstantValueState Unknown => new ConstantValueState(
        Kind: ConstantValueKind.Unknown,
        Value: null);

    public static ConstantValueState Overdefined => new ConstantValueState(
        Kind: ConstantValueKind.Overdefined,
        Value: null);

    public static ConstantValueState Constant(
        object? value)
    {
        return new ConstantValueState(
            Kind: ConstantValueKind.Constant,
            Value: value);
    }
}
