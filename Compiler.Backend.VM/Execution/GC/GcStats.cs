namespace Compiler.Backend.VM.Execution.GC;

public readonly struct GcStats(
    int totalAllocations,
    int collections,
    int peakLive,
    int live,
    int threshold,
    double growthFactor)
{
    public int Collections { get; } = collections;

    public double GrowthFactor { get; } = growthFactor;

    public int Live { get; } = live;

    public int PeakLive { get; } = peakLive;

    public int Threshold { get; } = threshold;

    public int TotalAllocations { get; } = totalAllocations;
}
