namespace Compiler.Frontend.Translation.MIR.Common;

public sealed record MirOptimizationOptions(
    MirOptimizationPasses EnabledPasses = MirOptimizationPasses.StableDefault,
    bool CollectPassDiagnostics = false,
    int MaxIterations = 6);
