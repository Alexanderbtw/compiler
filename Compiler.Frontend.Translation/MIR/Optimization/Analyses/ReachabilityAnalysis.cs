using Compiler.Frontend.Translation.MIR.Instructions;

namespace Compiler.Frontend.Translation.MIR.Optimization;

public sealed class ReachabilityAnalysis(
    ControlFlowGraph cfg)
{
    public IReadOnlySet<MirBlock> ReachableBlocks => cfg.ReachableBlocks;

    public bool IsReachable(
        MirBlock block)
    {
        return cfg.ReachableBlocks.Contains(block);
    }
}
