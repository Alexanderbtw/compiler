namespace Compiler.Runtime.VM.Bytecode;

/// <summary>
///     Compiled bytecode module executed by the VM.
/// </summary>
public sealed class VmProgram(
    IReadOnlyDictionary<string, VmFunction> functions)
{
    public IReadOnlyDictionary<string, VmFunction> Functions { get; } = functions;
}
