using Compiler.Frontend.Translation.MIR.Common;
using Compiler.Frontend.Translation.MIR.Instructions;

namespace Compiler.Frontend.Translation.MIR.Optimization;

public sealed class UnreachableBlockEliminationPass : IMirOptimizationPass
{
    public string Name => nameof(UnreachableBlockEliminationPass);

    public MirPassResult Run(
        MirFunction function,
        MirAnalysisManager analyses)
    {
        ReachabilityAnalysis reachability = analyses.GetReachabilityAnalysis();
        List<MirBlock> keptBlocks = function.Blocks
            .Where(reachability.IsReachable)
            .ToList();

        if (keptBlocks.Count == function.Blocks.Count)
        {
            return MirPassResult.NoChange;
        }

        function.MutableBlocks.Clear();
        function.MutableBlocks.AddRange(keptBlocks);

        return MirPassResult.ChangedAnalyses(MirAnalysisKind.All);
    }
}
