namespace Compiler.Backend.CLR.Tiering;

/// <summary>
///     Controls VM hot-function tiered JIT behavior.
/// </summary>
public sealed class VmJitOptions
{
    /// <summary>
    ///     Function invocation count required before switching to the CLR-compiled tier.
    /// </summary>
    public int FunctionHotThreshold { get; init; } = 2;
}
