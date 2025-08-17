namespace Compiler.Translation.MIR.Operands;

public sealed record VReg(int Id) : MOperand
{
    public override string ToString() => $"%t{Id}";
}