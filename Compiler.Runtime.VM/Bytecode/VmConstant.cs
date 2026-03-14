namespace Compiler.Runtime.VM.Bytecode;

public enum VmConstantKind
{
    Null,
    I64,
    Bool,
    Char,
    String
}

public readonly record struct VmConstant(
    VmConstantKind Kind,
    long Payload,
    string? Text = null)
{
    public static VmConstant FromBool(
        bool value)
    {
        return new VmConstant(
            Kind: VmConstantKind.Bool,
            Payload: value
                ? 1
                : 0);
    }

    public static VmConstant FromChar(
        char value)
    {
        return new VmConstant(
            Kind: VmConstantKind.Char,
            Payload: value);
    }

    public static VmConstant FromLong(
        long value)
    {
        return new VmConstant(
            Kind: VmConstantKind.I64,
            Payload: value);
    }

    public static VmConstant FromString(
        string value)
    {
        return new VmConstant(
            Kind: VmConstantKind.String,
            Payload: 0,
            Text: value);
    }

    public static VmConstant Null()
    {
        return new VmConstant(
            Kind: VmConstantKind.Null,
            Payload: 0);
    }
}
