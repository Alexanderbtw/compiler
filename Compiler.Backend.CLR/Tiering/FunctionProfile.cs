namespace Compiler.Backend.CLR.Tiering;

/// <summary>
///     Aggregated hotness/profile information for a VM function.
/// </summary>
public sealed class FunctionProfile(
    string functionName)
{
    /// <summary>
    ///     Current execution target.
    /// </summary>
    public FunctionExecutionTargetKind ActiveTargetKind { get; private set; }

    /// <summary>
    ///     Number of CLR-tier invocations served by the compiled target.
    /// </summary>
    public int CompiledInvocationCount { get; private set; }

    /// <summary>
    ///     Number of times the function has been compiled.
    /// </summary>
    public int CompilationCount { get; private set; }

    /// <summary>
    ///     Function name.
    /// </summary>
    public string FunctionName { get; } = functionName;

    /// <summary>
    ///     Number of VM frame entries observed for the function.
    /// </summary>
    public int InvocationCount { get; private set; }

    /// <summary>
    ///     Number of backward branches observed for the function.
    /// </summary>
    public int LoopBackEdgeCount { get; private set; }

    internal void MarkCompiled()
    {
        ActiveTargetKind = FunctionExecutionTargetKind.ClrCompiled;
        CompilationCount++;
    }

    internal void RecordCompiledInvocation()
    {
        CompiledInvocationCount++;
    }

    internal void RecordInvocation()
    {
        InvocationCount++;
    }

    internal void RecordLoopBackEdge()
    {
        LoopBackEdgeCount++;
    }
}
