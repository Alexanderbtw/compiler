using Compiler.Frontend.Translation.MIR.Common;
using Compiler.Frontend.Translation.MIR.Instructions;
using Compiler.Frontend.Translation.MIR.Instructions.Abstractions;
using Compiler.Frontend.Translation.MIR.Operands;
using Compiler.Frontend.Translation.MIR.Operands.Abstractions;

namespace Compiler.Frontend.Translation.MIR.Optimization;

public sealed class GlobalConstantPropagationPass : IMirOptimizationPass
{
    public string Name => nameof(GlobalConstantPropagationPass);

    public MirPassResult Run(
        MirFunction function,
        MirAnalysisManager analyses)
    {
        ConstantStateAnalysis constantState = analyses.GetConstantStateAnalysis();
        bool changed = false;

        foreach (MirBlock block in function.Blocks)
        {
            IReadOnlyList<ConstantEnvironment> inputs = constantState.GetInstructionInputs(block);

            for (var i = 0; i < block.Instructions.Count; i++)
            {
                MirInstr original = block.Instructions[i];
                MirInstr rewritten = RewriteInstruction(
                    instruction: original,
                    state: inputs[i]);

                if (rewritten != original)
                {
                    block.Instructions[i] = rewritten;
                    changed = true;
                }
            }

            if (block.Terminator is not null)
            {
                ConstantEnvironment terminatorState = constantState.GetTerminatorInput(block);
                MirInstr rewrittenTerminator = RewriteInstruction(
                    instruction: block.Terminator,
                    state: terminatorState);

                if (rewrittenTerminator != block.Terminator)
                {
                    block.Terminator = rewrittenTerminator;
                    changed = true;
                }
            }
        }

        return changed
            ? MirPassResult.ChangedAnalyses(MirAnalysisKind.ConstantState | MirAnalysisKind.Liveness)
            : MirPassResult.NoChange;
    }

    private static MirInstr RewriteInstruction(
        MirInstr instruction,
        ConstantEnvironment state)
    {
        MirInstr normalized = MirInstructionUtilities.ReplaceOperands(
            instruction: instruction,
            rewriteOperand: operand => RewriteOperand(
                state: state,
                operand: operand));

        return normalized switch
        {
            Bin binary when binary.L is Const leftConst &&
                            binary.R is Const rightConst &&
                            MirConstantEvaluator.TryEvaluateBinary(
                                op: binary.Op,
                                leftValue: leftConst.Value,
                                rightValue: rightConst.Value,
                                result: out object? folded) => new Move(
                Dst: binary.Dst,
                Src: new Const(folded)),
            Un unary when unary.X is Const constOperand &&
                          MirConstantEvaluator.TryEvaluateUnary(
                              op: unary.Op,
                              value: constOperand.Value,
                              result: out object? folded) => new Move(
                Dst: unary.Dst,
                Src: new Const(folded)),
            Call { Dst: not null } call when TryFoldCall(
                call: call,
                result: out object? folded) => new Move(
                Dst: call.Dst!,
                Src: new Const(folded)),
            _ => normalized
        };
    }

    private static MOperand RewriteOperand(
        ConstantEnvironment state,
        MOperand operand)
    {
        if (operand is not VReg register)
        {
            return operand;
        }

        ConstantValueState resolved = state.Get(register);

        return resolved.Kind == ConstantValueKind.Constant
            ? new Const(resolved.Value)
            : operand;
    }

    private static bool TryFoldCall(
        Call call,
        out object? result)
    {
        result = null;

        if (call.Args.Any(arg => arg is not Const))
        {
            return false;
        }

        object?[] args = call.Args
            .Cast<Const>()
            .Select(c => c.Value)
            .ToArray();

        return MirConstantEvaluator.TryEvaluateBuiltinCall(
            callee: call.Callee,
            args: args,
            result: out result);
    }
}
