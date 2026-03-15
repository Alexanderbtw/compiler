using Compiler.Runtime.VM.Bytecode;

namespace Compiler.Runtime.VM.Execution;

/// <summary>
///     Observes VM execution and can redirect eligible function calls to an alternate execution target.
/// </summary>
public interface IVmExecutionObserver
{
    /// <summary>
    ///     Notifies that a function frame has been entered.
    /// </summary>
    /// <param name="function">Entered function.</param>
    void OnFunctionEntered(
        VmFunction function);

    /// <summary>
    ///     Notifies that execution took a backward branch.
    /// </summary>
    /// <param name="function">Owning function.</param>
    /// <param name="sourceInstruction">Branch source instruction index.</param>
    /// <param name="targetInstruction">Branch target instruction index.</param>
    void OnLoopBackEdge(
        VmFunction function,
        int sourceInstruction,
        int targetInstruction);

    /// <summary>
    ///     Attempts to execute a function via an alternate target instead of the baseline bytecode interpreter.
    /// </summary>
    /// <param name="vm">Active VM runtime.</param>
    /// <param name="function">Function being invoked.</param>
    /// <param name="arguments">Call arguments.</param>
    /// <param name="result">Result when redirection succeeds.</param>
    /// <returns><see langword="true" /> when the call was handled by the observer.</returns>
    bool TryInvokeFunction(
        VirtualMachine vm,
        VmFunction function,
        VmValue[] arguments,
        out VmValue result);
}
