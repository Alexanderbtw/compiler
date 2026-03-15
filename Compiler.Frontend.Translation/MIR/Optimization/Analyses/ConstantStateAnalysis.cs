using Compiler.Frontend.Translation.HIR.Metadata;
using Compiler.Frontend.Translation.MIR.Common;
using Compiler.Frontend.Translation.MIR.Instructions;
using Compiler.Frontend.Translation.MIR.Instructions.Abstractions;
using Compiler.Frontend.Translation.MIR.Operands;
using Compiler.Frontend.Translation.MIR.Operands.Abstractions;

namespace Compiler.Frontend.Translation.MIR.Optimization;

public sealed class ConstantStateAnalysis
{
    private readonly Dictionary<MirBlock, ConstantEnvironment> _blockInputs = [];
    private readonly Dictionary<MirBlock, ConstantEnvironment> _blockOutputs = [];
    private readonly ControlFlowGraph _cfg;
    private readonly Dictionary<MirBlock, IReadOnlyList<ConstantEnvironment>> _instructionInputs = [];
    private readonly Dictionary<MirBlock, ConstantEnvironment> _terminatorInputs = [];

    public ConstantStateAnalysis(
        MirFunction function,
        ControlFlowGraph cfg)
    {
        _cfg = cfg;

        foreach (MirBlock block in function.Blocks)
        {
            _blockInputs[block] = new ConstantEnvironment();
            _blockOutputs[block] = new ConstantEnvironment();
            _instructionInputs[block] = [];
            _terminatorInputs[block] = new ConstantEnvironment();
        }

        Analyze(function);
    }

    public ConstantEnvironment GetBlockInput(
        MirBlock block)
    {
        return _blockInputs[block];
    }

    public ConstantEnvironment GetBlockOutput(
        MirBlock block)
    {
        return _blockOutputs[block];
    }

    public IReadOnlyList<ConstantEnvironment> GetInstructionInputs(
        MirBlock block)
    {
        return _instructionInputs[block];
    }

    public ConstantEnvironment GetTerminatorInput(
        MirBlock block)
    {
        return _terminatorInputs[block];
    }

    private void Analyze(
        MirFunction function)
    {
        var worklist = new Queue<MirBlock>(function.Blocks);
        HashSet<MirBlock> queued = [.. function.Blocks];

        while (worklist.TryDequeue(out MirBlock? block))
        {
            queued.Remove(block);

            if (!_cfg.ReachableBlocks.Contains(block))
            {
                continue;
            }

            ConstantEnvironment mergedInput = block == _cfg.Entry
                ? new ConstantEnvironment()
                : ConstantEnvironment.Merge(
                    _cfg.GetPredecessors(block)
                        .Where(_cfg.ReachableBlocks.Contains)
                        .Select(GetBlockOutput));

            ConstantEnvironment previousOutput = _blockOutputs[block];
            ConstantEnvironment state = mergedInput.Clone();
            var instructionInputs = new List<ConstantEnvironment>(block.Instructions.Count);

            foreach (MirInstr instruction in block.Instructions)
            {
                instructionInputs.Add(state.Clone());
                ApplyInstructionTransfer(
                    instruction: instruction,
                    state: state);
            }

            _blockInputs[block] = mergedInput;
            _instructionInputs[block] = instructionInputs;
            _terminatorInputs[block] = state.Clone();
            _blockOutputs[block] = state.Clone();

            if (!state.ContentEquals(previousOutput))
            {
                foreach (MirBlock successor in _cfg.GetSuccessors(block))
                {
                    if (queued.Add(successor))
                    {
                        worklist.Enqueue(successor);
                    }
                }
            }
        }
    }

    private static void ApplyInstructionTransfer(
        MirInstr instruction,
        ConstantEnvironment state)
    {
        switch (instruction)
        {
            case Move move:
                state.Set(
                    register: move.Dst,
                    state: ResolveState(
                        state: state,
                        operand: move.Src));
                break;
            case Bin binary:
                state.Set(
                    register: binary.Dst,
                    state: EvaluateBinary(
                        state: state,
                        instruction: binary));
                break;
            case Un unary:
                state.Set(
                    register: unary.Dst,
                    state: EvaluateUnary(
                        state: state,
                        instruction: unary));
                break;
            case Call call when call.Dst is not null:
                state.Set(
                    register: call.Dst,
                    state: EvaluateCall(
                        state: state,
                        instruction: call));
                break;
            case LoadIndex loadIndex:
                state.Set(
                    register: loadIndex.Dst,
                    state: ConstantValueState.Overdefined);
                break;
            case Phi phi:
                state.Set(
                    register: phi.Dst,
                    state: ConstantValueState.Overdefined);
                break;
        }
    }

    private static ConstantValueState EvaluateBinary(
        ConstantEnvironment state,
        Bin instruction)
    {
        ConstantValueState left = ResolveState(
            state: state,
            operand: instruction.L);
        ConstantValueState right = ResolveState(
            state: state,
            operand: instruction.R);

        if (left.Kind == ConstantValueKind.Unknown || right.Kind == ConstantValueKind.Unknown)
        {
            return ConstantValueState.Unknown;
        }

        if (left.Kind == ConstantValueKind.Overdefined || right.Kind == ConstantValueKind.Overdefined)
        {
            return ConstantValueState.Overdefined;
        }

        return MirConstantEvaluator.TryEvaluateBinary(
                op: instruction.Op,
                leftValue: left.Value,
                rightValue: right.Value,
                result: out object? result)
            ? ConstantValueState.Constant(result)
            : ConstantValueState.Overdefined;
    }

    private static ConstantValueState EvaluateCall(
        ConstantEnvironment state,
        Call instruction)
    {
        if (!Builtins.Table.TryGetValue(
                key: instruction.Callee,
                value: out List<BuiltinDescriptor>? descriptors))
        {
            return ConstantValueState.Overdefined;
        }

        BuiltinDescriptor? descriptor = descriptors.SingleOrDefault(d =>
            d.Attributes.HasFlag(BuiltinAttr.Foldable) && d.Attributes.HasFlag(BuiltinAttr.NoThrow));

        if (descriptor is null)
        {
            return ConstantValueState.Overdefined;
        }

        object?[] args = new object?[instruction.Args.Count];

        for (var i = 0; i < instruction.Args.Count; i++)
        {
            ConstantValueState arg = ResolveState(
                state: state,
                operand: instruction.Args[i]);

            if (arg.Kind == ConstantValueKind.Unknown)
            {
                return ConstantValueState.Unknown;
            }

            if (arg.Kind == ConstantValueKind.Overdefined)
            {
                return ConstantValueState.Overdefined;
            }

            args[i] = arg.Value;
        }

        return MirConstantEvaluator.TryEvaluateBuiltinCall(
                callee: instruction.Callee,
                args: args,
                result: out object? result)
            ? ConstantValueState.Constant(result)
            : ConstantValueState.Overdefined;
    }

    private static ConstantValueState EvaluateUnary(
        ConstantEnvironment state,
        Un instruction)
    {
        ConstantValueState operand = ResolveState(
            state: state,
            operand: instruction.X);

        if (operand.Kind == ConstantValueKind.Unknown)
        {
            return ConstantValueState.Unknown;
        }

        if (operand.Kind == ConstantValueKind.Overdefined)
        {
            return ConstantValueState.Overdefined;
        }

        return MirConstantEvaluator.TryEvaluateUnary(
                op: instruction.Op,
                value: operand.Value,
                result: out object? result)
            ? ConstantValueState.Constant(result)
            : ConstantValueState.Overdefined;
    }

    private static ConstantValueState ResolveState(
        ConstantEnvironment state,
        MOperand operand)
    {
        return operand switch
        {
            Const constant => ConstantValueState.Constant(constant.Value),
            VReg register => state.Get(register),
            _ => ConstantValueState.Overdefined
        };
    }
}
