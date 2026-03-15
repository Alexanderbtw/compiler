using Compiler.Frontend.Translation.MIR.Common;
using Compiler.Frontend.Translation.MIR.Instructions;
using Compiler.Frontend.Translation.MIR.Optimization.Analyses;
using Compiler.Frontend.Translation.MIR.Optimization.Infrastructure;

namespace Compiler.Frontend.Translation.MIR.Optimization.Passes;

public sealed class ControlFlowCleanupPass : IMirOptimizationPass
{
    public string Name => nameof(ControlFlowCleanupPass);

    public MirPassResult Run(
        MirFunction function,
        MirAnalysisManager analyses)
    {
        var changed = false;
        MirBlock entry = function.Blocks[0];

        Dictionary<MirBlock, MirBlock> redirects = BuildRedirects(
            function: function,
            entry: entry);

        if (redirects.Count > 0)
        {
            MirInstructionUtilities.ReplaceSuccessorTargets(
                function: function,
                rewriteTarget: target => ResolveRedirect(
                    redirects: redirects,
                    block: target));

            foreach ((MirBlock from, MirBlock to) in redirects)
            {
                MirInstructionUtilities.ReplacePhiIncomingBlocks(
                    function: function,
                    from: from,
                    to: to);
            }

            function.MutableBlocks.RemoveAll(redirects.ContainsKey);
            changed = true;
            analyses.Invalidate(MirAnalysisKind.All);
        }

        bool merged;

        do
        {
            merged = false;
            ControlFlowGraph cfg = analyses.GetControlFlowGraph();

            foreach (MirBlock block in function.Blocks.ToArray())
            {
                if (block.Terminator is not Br branch)
                {
                    continue;
                }

                MirBlock target = branch.Target;

                if (ReferenceEquals(
                        objA: block,
                        objB: target) ||
                    ReferenceEquals(
                        objA: target,
                        objB: entry) ||
                    cfg.GetPredecessors(target)
                        .Count != 1)
                {
                    continue;
                }

                block.Instructions.AddRange(target.Instructions);
                block.Terminator = target.Terminator;
                MirInstructionUtilities.ReplacePhiIncomingBlocks(
                    function: function,
                    from: target,
                    to: block);

                function.MutableBlocks.Remove(target);
                analyses.Invalidate(MirAnalysisKind.All);
                merged = true;
                changed = true;

                break;
            }
        }
        while (merged);

        return changed
            ? MirPassResult.ChangedAnalyses(MirAnalysisKind.All)
            : MirPassResult.NoChange;
    }

    private static Dictionary<MirBlock, MirBlock> BuildRedirects(
        MirFunction function,
        MirBlock entry)
    {
        var redirects = new Dictionary<MirBlock, MirBlock>();

        foreach (MirBlock block in function.Blocks)
        {
            if (ReferenceEquals(
                    objA: block,
                    objB: entry) ||
                block.Instructions.Count != 0 ||
                block.Terminator is not Br branch ||
                ReferenceEquals(
                    objA: block,
                    objB: branch.Target))
            {
                continue;
            }

            redirects[block] = branch.Target;
        }

        return redirects;
    }

    private static MirBlock ResolveRedirect(
        IReadOnlyDictionary<MirBlock, MirBlock> redirects,
        MirBlock block)
    {
        HashSet<MirBlock> visited = [];
        MirBlock current = block;

        while (redirects.TryGetValue(
                   key: current,
                   value: out MirBlock? next) && visited.Add(current))
        {
            current = next;
        }

        return current;
    }
}
