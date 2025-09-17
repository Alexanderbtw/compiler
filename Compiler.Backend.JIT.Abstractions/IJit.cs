using Compiler.Backend.VM;
using Compiler.Backend.VM.Values;
using Compiler.Frontend.Translation.MIR.Common;

namespace Compiler.Backend.JIT.Abstractions;

/// <summary>
///     Common JIT interface shared by backends.
///     Takes a VM host and MIR, returns a Value from the entry function.
/// </summary>
public interface IJit
{
    Value Execute(
        VirtualMachine virtualMachine,
        MirModule mirModule,
        string entryFunctionName);
}
