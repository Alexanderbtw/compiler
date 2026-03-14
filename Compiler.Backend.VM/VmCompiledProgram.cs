using Compiler.Runtime.VM;
using Compiler.Runtime.VM.Bytecode;

namespace Compiler.Backend.VM;

public sealed class VmCompiledProgram(
    VmProgram program)
{
    public VmValue Execute(
        VirtualMachine vm,
        string entryFunctionName)
    {
        return vm.Execute(
            program: program,
            entryFunctionName: entryFunctionName);
    }
}
