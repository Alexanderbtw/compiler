namespace Compiler.Backend.CLR.Tiering;

/// <summary>
///     Active execution target for a function.
/// </summary>
public enum FunctionExecutionTargetKind
{
    /// <summary>
    ///     Executes through the baseline bytecode interpreter.
    /// </summary>
    Baseline = 0,

    /// <summary>
    ///     Executes through the promoted CLR-compiled tier.
    /// </summary>
    ClrCompiled = 1
}
