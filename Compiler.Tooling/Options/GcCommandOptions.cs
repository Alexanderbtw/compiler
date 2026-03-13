namespace Compiler.Tooling.Options;

public sealed class GcCommandOptions
{
    public bool AutoCollect { get; set; } = true;

    public double GrowthFactor { get; set; } = 2.0;

    public int InitialThreshold { get; set; } = 1024;

    public bool PrintStats { get; set; }
}
