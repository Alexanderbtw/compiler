using Compiler.Frontend.Translation.MIR.Common;
using Compiler.Frontend.Translation.MIR.Instructions;
using Compiler.Frontend.Translation.MIR.Instructions.Abstractions;
using Compiler.Frontend.Translation.MIR.Optimization.Analyses;
using Compiler.Frontend.Translation.MIR.Optimization.Infrastructure;

namespace Compiler.Frontend.Translation.MIR.Optimization.Passes;

public sealed class DeadCodeEliminationPass : IMirOptimizationPass
{
    public string Name => nameof(DeadCodeEliminationPass);

    public MirPassResult Run(
        MirFunction function,
        MirAnalysisManager analyses)
    {
        ControlFlowGraph cfg = analyses.GetControlFlowGraph();
        LivenessAnalysis liveness = analyses.GetLivenessAnalysis();
        var changed = false;

        foreach (MirBlock block in function.Blocks)
        {
            if (!cfg.ReachableBlocks.Contains(block))
            {
                continue;
            }

            HashSet<int> live = [.. liveness.GetLiveOut(block)];

            if (block.Terminator is not null)
            {
                live.UnionWith(MirInstructionUtilities.GetUses(block.Terminator));
            }

            var rewritten = new List<MirInstr>(block.Instructions.Count);

            for (int i = block.Instructions.Count - 1; i >= 0; i--)
            {
                MirInstr instruction = block.Instructions[i];
                IReadOnlyList<int> defs = MirInstructionUtilities.GetDefs(instruction);
                bool hasLiveDef = defs.Any(live.Contains);
                bool canDelete = MirInstructionUtilities.IsDeletableInstruction(instruction);

                if (canDelete && defs.Count > 0 && !hasLiveDef)
                {
                    changed = true;
                    live.ExceptWith(defs);
                    live.UnionWith(MirInstructionUtilities.GetUses(instruction));

                    continue;
                }

                rewritten.Add(instruction);
                live.ExceptWith(defs);
                live.UnionWith(MirInstructionUtilities.GetUses(instruction));
            }

            rewritten.Reverse();
            block.Instructions.Clear();
            block.Instructions.AddRange(rewritten);
        }

        return changed
            ? MirPassResult.ChangedAnalyses(MirAnalysisKind.Liveness | MirAnalysisKind.ConstantState)
            : MirPassResult.NoChange;
    }
}
