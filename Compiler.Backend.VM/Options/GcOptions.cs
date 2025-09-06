namespace Compiler.Backend.VM.Options;

/// <summary>
///     Configuration for the VM's garbage collector.
/// </summary>
public sealed class GcOptions
{
    /// <summary>
    ///     Whether to trigger automatic collections when the threshold is reached.
    /// </summary>
    public bool AutoCollect { get; init; } = true;

    /// <summary>
    ///     A reusable default instance.
    /// </summary>
    public static GcOptions Default { get; } = new GcOptions();

    /// <summary>
    ///     Growth factor applied to the threshold after a collection that still leaves many objects live.
    /// </summary>
    public double GrowthFactor { get; init; } = 2.0;

    /// <summary>
    ///     Initial threshold (in number of live VM arrays) that triggers a collection after an allocation.
    /// </summary>
    public int InitialThreshold { get; init; } = 1024;
}
