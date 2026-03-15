namespace Compiler.Frontend.Translation.MIR.Common;

public sealed record MirOptimizationOptions(
    MirOptimizationLevel Level,
    bool CollectPassDiagnostics = false);
