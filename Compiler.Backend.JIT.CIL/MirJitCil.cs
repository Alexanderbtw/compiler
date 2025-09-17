using Compiler.Backend.JIT.Abstractions;
using Compiler.Backend.VM;
using Compiler.Backend.VM.Execution;
using Compiler.Backend.VM.Values;
using Compiler.Frontend.Translation.MIR.Common;

namespace Compiler.Backend.JIT.CIL;

/// <summary>
///     Thin host around CIL JIT
/// </summary>
public sealed class MirJitCil : IJit
{
    public Value Execute(
        VirtualMachine virtualMachine,
        MirModule mirModule,
        string entryFunctionName)
    {
        var context = new VmJitContext(vm: virtualMachine);
        CilEmitter.CilModule cilModule = CilEmitter.EmitModule(mirModule);

        foreach (KeyValuePair<string, VmJitFunc> kv in cilModule.Functions)
        {
            context.Register(
                name: kv.Key,
                fn: kv.Value);
        }

        if (!context.Functions.TryGetValue(
                key: entryFunctionName,
                value: out VmJitFunc? entryFunction))
        {
            throw new InvalidOperationException($"entry '{entryFunctionName}' not found");
        }

        return entryFunction(
            ctx: context,
            args: []);
    }
}
