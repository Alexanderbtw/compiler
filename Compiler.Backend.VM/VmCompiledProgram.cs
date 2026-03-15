using Compiler.Runtime.VM;
using Compiler.Runtime.VM.Bytecode;

namespace Compiler.Backend.VM;

public sealed class VmCompiledProgram
{
    public VmCompiledProgram(
        VmProgram program)
    {
        Program = program;
    }

    /// <summary>
    ///     Underlying bytecode module.
    /// </summary>
    public VmProgram Program { get; }

    /// <summary>
    ///     Executes the program on the baseline VM.
    /// </summary>
    /// <param name="vm">VM runtime.</param>
    /// <param name="entryFunctionName">Entry function name.</param>
    /// <returns>Program result.</returns>
    public VmValue Execute(
        VirtualMachine vm,
        string entryFunctionName)
    {
        return vm.Execute(
            program: Program,
            entryFunctionName: entryFunctionName);
    }
}
