namespace Compiler.Frontend.Translation.MIR.Common;

public sealed class MirOptimizationReport
{
    private readonly List<MirPassExecution> _passes = [];

    public MirOptimizationReport(
        MirOptimizationOptions options)
        : this(
            enabledPasses: options.EnabledPasses,
            level: options.Level)
    {
    }

    public MirOptimizationReport(
        MirOptimizationPasses enabledPasses,
        MirOptimizationLevel level)
    {
        EnabledPasses = enabledPasses;
        Level = level;
    }

    public MirOptimizationPasses EnabledPasses { get; }

    public MirOptimizationLevel Level { get; }

    public IReadOnlyList<MirPassExecution> Passes => _passes;

    internal void AddPass(
        MirPassExecution execution)
    {
        _passes.Add(execution);
    }
}
