using Compiler.Execution;

namespace Compiler.Backend.JIT.CIL;

internal sealed class CilCompiledProgram(
    IReadOnlyDictionary<string, CilJitFunc> functions) : ICompiledProgram
{
    public Value Execute(
        IExecutionRuntime runtime,
        string entryFunctionName)
    {
        var context = new CilExecutionContext(runtime);

        foreach (KeyValuePair<string, CilJitFunc> kv in functions)
        {
            context.Register(
                name: kv.Key,
                fn: kv.Value);
        }

        if (!context.TryGetFunction(
                name: entryFunctionName,
                fn: out CilJitFunc? entryFunction))
        {
            throw new InvalidOperationException($"entry '{entryFunctionName}' not found");
        }

        return entryFunction!(
            ctx: context,
            args: []);
    }
}
