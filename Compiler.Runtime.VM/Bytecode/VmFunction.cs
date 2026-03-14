namespace Compiler.Runtime.VM.Bytecode;

/// <summary>
///     Flat register-based function body executed by the VM.
/// </summary>
public sealed class VmFunction(
    string name,
    int registerCount,
    int parameterCount,
    IReadOnlyList<int> parameterRegisters,
    IReadOnlyList<VmInstruction> instructions,
    IReadOnlyList<VmConstant> constants)
{
    public IReadOnlyList<VmConstant> Constants { get; } = constants;

    public IReadOnlyList<VmInstruction> Instructions { get; } = instructions;

    public string Name { get; } = name;

    public int ParameterCount { get; } = parameterCount;

    public IReadOnlyList<int> ParameterRegisters { get; } = parameterRegisters;

    public int RegisterCount { get; } = registerCount;
}
