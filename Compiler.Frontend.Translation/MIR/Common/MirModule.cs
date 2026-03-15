namespace Compiler.Frontend.Translation.MIR.Common;

/// <summary>
///     bag of functions
/// </summary>
public sealed class MirModule
{
    public List<MirFunction> Functions { get; } = [];

    public MirOptimizationReport? OptimizationReport { get; internal set; }

    public override string ToString()
    {
        return string.Join(
            separator: "\n\n",
            values: Functions.Select(f => f.ToString()));
    }
}
