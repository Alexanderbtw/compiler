using Compiler.Frontend.Translation.MIR.Common;
using Compiler.Frontend.Translation.MIR.Instructions;

namespace Compiler.Frontend.Translation.MIR.Optimization;

public sealed class ControlFlowGraph
{
    private readonly Dictionary<MirBlock, IReadOnlyList<MirBlock>> _predecessors;
    private readonly Dictionary<MirBlock, IReadOnlyList<MirBlock>> _successors;

    public ControlFlowGraph(
        MirFunction function)
    {
        Function = function;
        Entry = function.Blocks[0];

        var predecessors = function.Blocks.ToDictionary(
            keySelector: block => block,
            elementSelector: _ => (ICollection<MirBlock>)new List<MirBlock>());

        var successors = function.Blocks.ToDictionary(
            keySelector: block => block,
            elementSelector: GetBlockSuccessors);

        foreach ((MirBlock block, IReadOnlyList<MirBlock> blockSuccessors) in successors)
        {
            foreach (MirBlock successor in blockSuccessors)
            {
                if (predecessors.TryGetValue(
                        key: successor,
                        value: out ICollection<MirBlock>? refs))
                {
                    refs.Add(block);
                }
            }
        }

        _predecessors = predecessors.ToDictionary(
            keySelector: pair => pair.Key,
            elementSelector: pair => (IReadOnlyList<MirBlock>)pair.Value.ToList());
        _successors = successors;
        ReachableBlocks = ComputeReachableBlocks();
    }

    public MirFunction Function { get; }

    public MirBlock Entry { get; }

    public IReadOnlySet<MirBlock> ReachableBlocks { get; }

    public IReadOnlyList<MirBlock> GetPredecessors(
        MirBlock block)
    {
        return _predecessors.TryGetValue(
            key: block,
            value: out IReadOnlyList<MirBlock>? predecessors)
            ? predecessors
            : Array.Empty<MirBlock>();
    }

    public IReadOnlyList<MirBlock> GetSuccessors(
        MirBlock block)
    {
        return _successors.TryGetValue(
            key: block,
            value: out IReadOnlyList<MirBlock>? successors)
            ? successors
            : Array.Empty<MirBlock>();
    }

    private IReadOnlySet<MirBlock> ComputeReachableBlocks()
    {
        HashSet<MirBlock> reachable = [];
        var worklist = new Queue<MirBlock>();
        worklist.Enqueue(Entry);

        while (worklist.TryDequeue(out MirBlock? block))
        {
            if (!reachable.Add(block))
            {
                continue;
            }

            foreach (MirBlock successor in GetSuccessors(block))
            {
                worklist.Enqueue(successor);
            }
        }

        return reachable;
    }

    private static IReadOnlyList<MirBlock> GetBlockSuccessors(
        MirBlock block)
    {
        return block.Terminator switch
        {
            Br branch => [branch.Target],
            BrCond branchCondition => [branchCondition.IfTrue, branchCondition.IfFalse],
            _ => Array.Empty<MirBlock>()
        };
    }
}
