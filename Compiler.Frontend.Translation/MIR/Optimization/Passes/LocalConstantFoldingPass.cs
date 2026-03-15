using Compiler.Frontend.Translation.HIR.Metadata;
using Compiler.Frontend.Translation.MIR.Common;
using Compiler.Frontend.Translation.MIR.Instructions;
using Compiler.Frontend.Translation.MIR.Instructions.Abstractions;
using Compiler.Frontend.Translation.MIR.Operands;
using Compiler.Frontend.Translation.MIR.Optimization.Infrastructure;

namespace Compiler.Frontend.Translation.MIR.Optimization.Passes;

public sealed class LocalConstantFoldingPass : IMirOptimizationPass
{
    public string Name => nameof(LocalConstantFoldingPass);

    public MirPassResult Run(
        MirFunction function,
        MirAnalysisManager analyses)
    {
        var changed = false;

        foreach (MirBlock block in function.Blocks)
        {
            for (var i = 0; i < block.Instructions.Count; i++)
            {
                MirInstr instruction = block.Instructions[i];
                MirInstr rewritten = FoldInstruction(instruction);

                if (rewritten != instruction)
                {
                    block.Instructions[i] = rewritten;
                    changed = true;
                }
            }
        }

        return changed
            ? MirPassResult.ChangedAnalyses(MirAnalysisKind.ConstantState | MirAnalysisKind.Liveness)
            : MirPassResult.NoChange;
    }

    private static MirInstr FoldInstruction(
        MirInstr instruction)
    {
        return instruction switch
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
            _ => instruction
        };
    }

    private static bool TryFoldCall(
        Call call,
        out object? result)
    {
        result = null;

        if (!Builtins.Table.TryGetValue(
                key: call.Callee,
                value: out List<BuiltinDescriptor>? descriptors) ||
            !descriptors.Any(descriptor => descriptor.Attributes.HasFlag(BuiltinAttr.Foldable) &&
                descriptor.Attributes.HasFlag(BuiltinAttr.NoThrow)))
        {
            return false;
        }

        if (call.Args.Any(arg => arg is not Const))
        {
            return false;
        }

        object?[] args = call
            .Args
            .Cast<Const>()
            .Select(constant => constant.Value)
            .ToArray();

        return MirConstantEvaluator.TryEvaluateBuiltinCall(
            callee: call.Callee,
            args: args,
            result: out result);
    }
}
