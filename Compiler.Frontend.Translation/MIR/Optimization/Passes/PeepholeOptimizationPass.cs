using Compiler.Frontend.Translation.MIR.Common;
using Compiler.Frontend.Translation.MIR.Instructions;
using Compiler.Frontend.Translation.MIR.Operands;
using Compiler.Frontend.Translation.MIR.Operands.Abstractions;
using Compiler.Frontend.Translation.MIR.Optimization.Infrastructure;

namespace Compiler.Frontend.Translation.MIR.Optimization.Passes;

public sealed class PeepholeOptimizationPass : IMirOptimizationPass
{
    public string Name => nameof(PeepholeOptimizationPass);

    public MirPassResult Run(
        MirFunction function,
        MirAnalysisManager analyses)
    {
        var changed = false;

        foreach (MirBlock block in function.Blocks)
        {
            for (int i = block.Instructions.Count - 1; i >= 0; i--)
            {
                if (block.Instructions[i] is Move
                    {
                        Dst.Id: var dstId,
                        Src: VReg
                        {
                            Id: var srcId
                        }
                    } && dstId == srcId)
                {
                    block.Instructions.RemoveAt(i);
                    changed = true;
                }
            }

            if (block.Instructions.Count == 0 || block.Terminator is null)
            {
                continue;
            }

            if (block.Instructions[^1] is not Move move)
            {
                continue;
            }

            MOperand? replacement = block.Terminator switch
            {
                Ret
                {
                    Value: VReg register
                } ret when register.Id == move.Dst.Id => move.Src,
                BrCond
                {
                    Cond: VReg register
                } branchCondition when register.Id == move.Dst.Id => move.Src,
                _ => null
            };

            if (replacement is null)
            {
                continue;
            }

            block.Terminator = block.Terminator switch
            {
                Ret => new Ret(replacement),
                BrCond branchCondition => new BrCond(
                    Cond: replacement,
                    IfTrue: branchCondition.IfTrue,
                    IfFalse: branchCondition.IfFalse),
                _ => block.Terminator
            };

            changed = true;
        }

        return changed
            ? MirPassResult.ChangedAnalyses(MirAnalysisKind.ConstantState | MirAnalysisKind.Liveness)
            : MirPassResult.NoChange;
    }
}
