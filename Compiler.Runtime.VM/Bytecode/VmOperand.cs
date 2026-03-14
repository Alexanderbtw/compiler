namespace Compiler.Runtime.VM.Bytecode;

public enum VmOperandKind
{
    Register,
    Constant
}

public readonly record struct VmOperand(
    VmOperandKind Kind,
    int Index)
{
    public static VmOperand Constant(
        int index)
    {
        return new VmOperand(
            Kind: VmOperandKind.Constant,
            Index: index);
    }

    public static VmOperand Register(
        int index)
    {
        return new VmOperand(
            Kind: VmOperandKind.Register,
            Index: index);
    }
}
