namespace Compiler.Frontend.Translation.MIR.Common;

public sealed class MirOptimizationReport(
    MirOptimizationLevel level)
{
    private readonly List<MirPassExecution> _passes = [];

    public MirOptimizationLevel Level { get; } = level;

    public IReadOnlyList<MirPassExecution> Passes => _passes;

    internal void AddPass(
        MirPassExecution execution)
    {
        _passes.Add(execution);
    }
}
