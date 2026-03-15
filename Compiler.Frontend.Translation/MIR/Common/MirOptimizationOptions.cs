namespace Compiler.Frontend.Translation.MIR.Common;

public sealed class MirOptimizationOptions
{
    public MirOptimizationOptions(
        MirOptimizationPasses enabledPasses = MirOptimizationPasses.StableDefault,
        bool collectPassDiagnostics = false,
        int maxIterations = 6)
    {
        EnabledPasses = enabledPasses;
        CollectPassDiagnostics = collectPassDiagnostics;
        MaxIterations = maxIterations;
        Level = enabledPasses == MirOptimizationPasses.None
            ? MirOptimizationLevel.O0
            : MirOptimizationLevel.O1;
    }

    public MirOptimizationOptions(
        MirOptimizationLevel level,
        bool collectPassDiagnostics = false,
        int maxIterations = 6)
        : this(
            enabledPasses: level == MirOptimizationLevel.O0
                ? MirOptimizationPasses.None
                : MirOptimizationPasses.StableDefault,
            collectPassDiagnostics: collectPassDiagnostics,
            maxIterations: maxIterations)
    {
        Level = level;
    }

    public MirOptimizationPasses EnabledPasses { get; }

    public bool CollectPassDiagnostics { get; }

    public MirOptimizationLevel Level { get; }

    public int MaxIterations { get; }
}
