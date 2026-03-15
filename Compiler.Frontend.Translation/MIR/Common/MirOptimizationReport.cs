namespace Compiler.Frontend.Translation.MIR.Common;

public sealed class MirOptimizationReport(
    MirOptimizationPasses enabledPasses)
{
    private readonly List<MirPassExecution> _passes = [];

    public MirOptimizationPasses EnabledPasses { get; } = enabledPasses;

    public IReadOnlyList<MirPassExecution> Passes => _passes;

    internal void AddPass(
        MirPassExecution execution)
    {
        _passes.Add(execution);
    }
}
