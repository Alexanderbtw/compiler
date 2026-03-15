using Compiler.Runtime.VM;
using Compiler.Runtime.VM.Execution;

namespace Compiler.Backend.CLR;

/// <summary>
///     CLR-compiled program that preserves VM value semantics while executing via delegates generated at runtime.
/// </summary>
public sealed class VmClrCompiledProgram
{
    private readonly IReadOnlyDictionary<string, VmClrJitFunc> _functions;

    internal VmClrCompiledProgram(
        IReadOnlyDictionary<string, VmClrJitFunc> functions)
    {
        _functions = functions;
    }

    /// <summary>
    ///     Executes the compiled program.
    /// </summary>
    /// <param name="runtime">Runtime services backing the execution.</param>
    /// <param name="entryFunctionName">Entry function name.</param>
    /// <returns>The final return value.</returns>
    public VmValue Execute(
        IVmExecutionRuntime runtime,
        string entryFunctionName)
    {
        return Invoke(
            runtime: runtime,
            functionName: entryFunctionName,
            args: []);
    }

    /// <summary>
    ///     Invokes a specific compiled function with explicit arguments.
    /// </summary>
    /// <param name="runtime">Runtime services backing the execution.</param>
    /// <param name="functionName">Function name to invoke.</param>
    /// <param name="args">Call arguments.</param>
    /// <returns>Function result.</returns>
    public VmValue Invoke(
        IVmExecutionRuntime runtime,
        string functionName,
        VmValue[] args)
    {
        ArgumentNullException.ThrowIfNull(runtime);
        ArgumentException.ThrowIfNullOrWhiteSpace(functionName);
        ArgumentNullException.ThrowIfNull(args);

        var context = new VmClrExecutionContext(runtime);

        foreach ((string registeredFunctionName, VmClrJitFunc function) in _functions)
        {
            context.Register(
                name: registeredFunctionName,
                function: function);
        }

        if (!context.TryGetFunction(
                name: functionName,
                function: out VmClrJitFunc? entryFunction))
        {
            throw new InvalidOperationException($"entry '{functionName}' not found");
        }

        try
        {
            return entryFunction!(
                context: context,
                args: args);
        }
        finally
        {
            context.ResetFrames();
        }
    }
}
