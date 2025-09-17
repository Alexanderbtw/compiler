namespace Compiler.Backend.VM.Execution.GC;

/// <summary>
///     Snapshot of GC counters for the VM heap.
///     Intended for diagnostics and CLI stats; values are cheap to compute.
/// </summary>
public readonly struct GcStats(
    int totalAllocations,
    int collections,
    int peakLive,
    int live,
    int threshold,
    double growthFactor)
{
    /// <summary>Total number of collections performed so far.</summary>
    public int Collections { get; } = collections;

    /// <summary>Current growth factor used to raise the collection threshold after a collection.</summary>
    public double GrowthFactor { get; } = growthFactor;

    /// <summary>Current number of arrays considered live after the last collection.</summary>
    public int Live { get; } = live;

    /// <summary>Maximum number of simultaneously live arrays observed so far.</summary>
    public int PeakLive { get; } = peakLive;

    /// <summary>Current collection threshold that triggers a collection.</summary>
    public int Threshold { get; } = threshold;

    /// <summary>Total number of array allocations since the heap was created.</summary>
    public int TotalAllocations { get; } = totalAllocations;
}
