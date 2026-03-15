using Compiler.Frontend.Translation.MIR.Common;
using Compiler.Frontend.Translation.MIR.Instructions;
using Compiler.Frontend.Translation.MIR.Operands;

namespace Compiler.Frontend.Translation.MIR.Optimization;

public sealed class BranchFoldingPass : IMirOptimizationPass
{
    public string Name => nameof(BranchFoldingPass);

    public MirPassResult Run(
        MirFunction function,
        MirAnalysisManager analyses)
    {
        ConstantStateAnalysis constants = analyses.GetConstantStateAnalysis();
        bool changed = false;

        foreach (MirBlock block in function.Blocks)
        {
            if (block.Terminator is not BrCond branchCondition)
            {
                continue;
            }

            ConstantEnvironment state = constants.GetTerminatorInput(block);
            object? value = branchCondition.Cond switch
            {
                Const constant => constant.Value,
                VReg register when state.Get(register).Kind == ConstantValueKind.Constant => state.Get(register).Value,
                _ => null
            };

            if (value is not bool condition)
            {
                continue;
            }

            block.Terminator = new Br(condition
                ? branchCondition.IfTrue
                : branchCondition.IfFalse);
            changed = true;
        }

        return changed
            ? MirPassResult.ChangedAnalyses(MirAnalysisKind.ControlFlowGraph | MirAnalysisKind.Reachability | MirAnalysisKind.ConstantState | MirAnalysisKind.Liveness)
            : MirPassResult.NoChange;
    }
}
