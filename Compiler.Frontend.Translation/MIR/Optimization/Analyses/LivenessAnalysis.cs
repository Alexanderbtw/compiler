using Compiler.Frontend.Translation.MIR.Common;
using Compiler.Frontend.Translation.MIR.Instructions;
using Compiler.Frontend.Translation.MIR.Instructions.Abstractions;
using Compiler.Frontend.Translation.MIR.Optimization.Infrastructure;

namespace Compiler.Frontend.Translation.MIR.Optimization.Analyses;

public sealed class LivenessAnalysis
{
    private readonly Dictionary<MirBlock, HashSet<int>> _liveIn = [];
    private readonly Dictionary<MirBlock, HashSet<int>> _liveOut = [];

    public LivenessAnalysis(
        MirFunction function,
        ControlFlowGraph cfg)
    {
        foreach (MirBlock block in function.Blocks)
        {
            _liveIn[block] = [];
            _liveOut[block] = [];
        }

        bool changed;

        do
        {
            changed = false;

            foreach (MirBlock block in function.Blocks.Reverse())
            {
                if (!cfg.ReachableBlocks.Contains(block))
                {
                    continue;
                }

                HashSet<int> oldIn = [.. _liveIn[block]];
                HashSet<int> oldOut = [.. _liveOut[block]];

                HashSet<int> liveOut = [];

                foreach (MirBlock successor in cfg.GetSuccessors(block))
                {
                    liveOut.UnionWith(_liveIn[successor]);
                }

                HashSet<int> use = [];
                HashSet<int> def = [];

                foreach (MirInstr instruction in block.Instructions)
                {
                    foreach (int registerId in MirInstructionUtilities.GetUses(instruction))
                    {
                        if (!def.Contains(registerId))
                        {
                            use.Add(registerId);
                        }
                    }

                    foreach (int registerId in MirInstructionUtilities.GetDefs(instruction))
                    {
                        def.Add(registerId);
                    }
                }

                if (block.Terminator is not null)
                {
                    foreach (int registerId in MirInstructionUtilities.GetUses(block.Terminator))
                    {
                        if (!def.Contains(registerId))
                        {
                            use.Add(registerId);
                        }
                    }
                }

                HashSet<int> liveIn = [.. liveOut];
                liveIn.ExceptWith(def);
                liveIn.UnionWith(use);

                _liveOut[block] = liveOut;
                _liveIn[block] = liveIn;

                if (!liveIn.SetEquals(oldIn) || !liveOut.SetEquals(oldOut))
                {
                    changed = true;
                }
            }
        }
        while (changed);
    }

    public IReadOnlySet<int> GetLiveOut(
        MirBlock block)
    {
        return _liveOut[block];
    }
}
