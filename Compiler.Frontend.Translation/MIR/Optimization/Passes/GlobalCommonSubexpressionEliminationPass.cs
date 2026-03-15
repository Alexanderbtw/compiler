using Compiler.Frontend.Translation.MIR.Common;
using Compiler.Frontend.Translation.MIR.Instructions;
using Compiler.Frontend.Translation.MIR.Instructions.Abstractions;
using Compiler.Frontend.Translation.MIR.Operands;
using Compiler.Frontend.Translation.MIR.Operands.Abstractions;
using Compiler.Frontend.Translation.MIR.Optimization.Analyses;
using Compiler.Frontend.Translation.MIR.Optimization.Infrastructure;

namespace Compiler.Frontend.Translation.MIR.Optimization.Passes;

public sealed class GlobalCommonSubexpressionEliminationPass : IMirOptimizationPass
{
    public string Name => nameof(GlobalCommonSubexpressionEliminationPass);

    public MirPassResult Run(
        MirFunction function,
        MirAnalysisManager analyses)
    {
        ControlFlowGraph cfg = analyses.GetControlFlowGraph();
        Dictionary<MirBlock, Dictionary<ExpressionKey, int>> inStates = [];
        Dictionary<MirBlock, Dictionary<ExpressionKey, int>> outStates = [];

        foreach (MirBlock block in function.Blocks)
        {
            inStates[block] = [];
            outStates[block] = [];
        }

        var worklist = new Queue<MirBlock>(cfg.ReachableBlocks);
        HashSet<MirBlock> queued = [.. cfg.ReachableBlocks];

        while (worklist.TryDequeue(out MirBlock? block))
        {
            queued.Remove(block);

            Dictionary<ExpressionKey, int> input = ReferenceEquals(
                objA: block,
                objB: cfg.Entry)
                ? []
                : Intersect(
                    cfg
                        .GetPredecessors(block)
                        .Where(cfg.ReachableBlocks.Contains)
                        .Select(pred => outStates[pred]));

            Dictionary<ExpressionKey, int> output = Transfer(
                block: block,
                input: input);

            if (!DictionaryEquals(
                    left: output,
                    right: outStates[block]) || !DictionaryEquals(
                    left: input,
                    right: inStates[block]))
            {
                inStates[block] = input;
                outStates[block] = output;

                foreach (MirBlock successor in cfg.GetSuccessors(block))
                {
                    if (queued.Add(successor))
                    {
                        worklist.Enqueue(successor);
                    }
                }
            }
        }

        var changed = false;

        foreach (MirBlock block in function.Blocks)
        {
            if (!cfg.ReachableBlocks.Contains(block))
            {
                continue;
            }

            var state = new Dictionary<ExpressionKey, int>(inStates[block]);

            for (var i = 0; i < block.Instructions.Count; i++)
            {
                MirInstr instruction = block.Instructions[i];
                ExpressionKey? expression = TryCreateKey(instruction);

                if (expression is not null &&
                    TryGetDestinationRegisterId(
                        instruction: instruction,
                        registerId: out int destinationRegisterId) &&
                    state.TryGetValue(
                        key: expression,
                        value: out int availableRegisterId))
                {
                    if (availableRegisterId != destinationRegisterId)
                    {
                        block.Instructions[i] = new Move(
                            Dst: new VReg(destinationRegisterId),
                            Src: new VReg(availableRegisterId));

                        changed = true;
                    }
                }

                ApplyTransfer(
                    state: state,
                    instruction: block.Instructions[i]);
            }
        }

        return changed
            ? MirPassResult.ChangedAnalyses(MirAnalysisKind.ConstantState | MirAnalysisKind.Liveness)
            : MirPassResult.NoChange;
    }

    private static void ApplyTransfer(
        Dictionary<ExpressionKey, int> state,
        MirInstr instruction)
    {
        if (instruction is StoreIndex or Call)
        {
            state.Clear();

            return;
        }

        foreach (int def in MirInstructionUtilities.GetDefs(instruction))
        {
            KillExpressions(
                state: state,
                registerId: def);
        }

        ExpressionKey? key = TryCreateKey(instruction);

        if (key is not null &&
            TryGetDestinationRegisterId(
                instruction: instruction,
                registerId: out int registerId))
        {
            state[key] = registerId;
        }
    }

    private static (MOperand Left, MOperand Right) CanonicalizeBinaryOperands(
        MBinOp op,
        MOperand left,
        MOperand right)
    {
        if (op is not (MBinOp.Add or MBinOp.Mul or MBinOp.Eq or MBinOp.Ne))
        {
            return (left, right);
        }

        var leftKey = left.ToString();
        var rightKey = right.ToString();

        return string.CompareOrdinal(
            strA: leftKey,
            strB: rightKey) <= 0
            ? (left, right)
            : (right, left);
    }

    private static bool DictionaryEquals(
        IReadOnlyDictionary<ExpressionKey, int> left,
        IReadOnlyDictionary<ExpressionKey, int> right)
    {
        if (left.Count != right.Count)
        {
            return false;
        }

        foreach ((ExpressionKey key, int value) in left)
        {
            if (!right.TryGetValue(
                    key: key,
                    value: out int otherValue) || otherValue != value)
            {
                return false;
            }
        }

        return true;
    }

    private static Dictionary<ExpressionKey, int> Intersect(
        IEnumerable<Dictionary<ExpressionKey, int>> predecessorStates)
    {
        Dictionary<ExpressionKey, int>[] states = predecessorStates.ToArray();

        if (states.Length == 0)
        {
            return [];
        }

        var result = new Dictionary<ExpressionKey, int>(states[0]);

        foreach ((ExpressionKey key, int value) in states[0])
        {
            if (states
                .Skip(1)
                .Any(state => !state.TryGetValue(
                    key: key,
                    value: out int otherValue) || otherValue != value))
            {
                result.Remove(key);
            }
        }

        return result;
    }

    private static bool IsSupportedBinary(
        MBinOp op)
    {
        return op is MBinOp.Add or MBinOp.Sub or MBinOp.Mul or MBinOp.Eq or MBinOp.Ne or MBinOp.Lt or MBinOp.Le or MBinOp.Gt or MBinOp.Ge;
    }

    private static void KillExpressions(
        Dictionary<ExpressionKey, int> state,
        int registerId)
    {
        foreach (ExpressionKey key in state
                     .Keys
                     .Where(key => key.UsesRegister(registerId) || state[key] == registerId)
                     .ToArray())
        {
            state.Remove(key);
        }
    }

    private static Dictionary<ExpressionKey, int> Transfer(
        MirBlock block,
        Dictionary<ExpressionKey, int> input)
    {
        var state = new Dictionary<ExpressionKey, int>(input);

        foreach (MirInstr instruction in block.Instructions)
        {
            ApplyTransfer(
                state: state,
                instruction: instruction);
        }

        return state;
    }

    private static ExpressionKey? TryCreateKey(
        MirInstr instruction)
    {
        switch (instruction)
        {
            case Bin binary when IsSupportedBinary(binary.Op):
                (MOperand left, MOperand right) = CanonicalizeBinaryOperands(
                    op: binary.Op,
                    left: binary.L,
                    right: binary.R);

                return ExpressionKey.Binary(
                    op: binary.Op,
                    left: left,
                    right: right);
            case Un unary when unary.Op is MUnOp.Plus or MUnOp.Neg or MUnOp.Not:
                return ExpressionKey.Unary(
                    op: unary.Op,
                    operand: unary.X);
            default:
                return null;
        }
    }

    private static bool TryGetDestinationRegisterId(
        MirInstr instruction,
        out int registerId)
    {
        registerId = instruction switch
        {
            Bin binary => binary.Dst.Id,
            Un unary => unary.Dst.Id,
            _ => -1
        };

        return registerId >= 0;
    }

    private sealed record ExpressionKey(
        string Kind,
        string Op,
        string Left,
        string? Right)
    {
        public static ExpressionKey Binary(
            MBinOp op,
            MOperand left,
            MOperand right)
        {
            return new ExpressionKey(
                Kind: "bin",
                Op: op.ToString(),
                Left: OperandKey(left),
                Right: OperandKey(right));
        }

        public static ExpressionKey Unary(
            MUnOp op,
            MOperand operand)
        {
            return new ExpressionKey(
                Kind: "un",
                Op: op.ToString(),
                Left: OperandKey(operand),
                Right: null);
        }

        public bool UsesRegister(
            int registerId)
        {
            var needle = $"r:{registerId}";

            return Left == needle || Right == needle;
        }

        private static string OperandKey(
            MOperand operand)
        {
            return operand switch
            {
                VReg register => $"r:{register.Id}",
                Const constant => $"c:{constant.Value?.GetType().FullName ?? "null"}:{constant.Value}",
                _ => operand.ToString()
            };
        }
    }
}
