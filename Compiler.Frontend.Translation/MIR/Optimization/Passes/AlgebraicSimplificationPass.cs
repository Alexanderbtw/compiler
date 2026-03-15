using Compiler.Frontend.Translation.MIR.Common;
using Compiler.Frontend.Translation.MIR.Instructions;
using Compiler.Frontend.Translation.MIR.Instructions.Abstractions;
using Compiler.Frontend.Translation.MIR.Operands;
using Compiler.Frontend.Translation.MIR.Operands.Abstractions;
using Compiler.Frontend.Translation.MIR.Optimization.Infrastructure;

namespace Compiler.Frontend.Translation.MIR.Optimization.Passes;

public sealed class AlgebraicSimplificationPass : IMirOptimizationPass
{
    public string Name => nameof(AlgebraicSimplificationPass);

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
                MirInstr rewritten = SimplifyInstruction(instruction);

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

    private static bool IsOne(
        MOperand operand)
    {
        return operand is Const { Value: 1L };
    }

    private static bool IsZero(
        MOperand operand)
    {
        return operand is Const { Value: 0L };
    }

    private static MirInstr SimplifyInstruction(
        MirInstr instruction)
    {
        if (instruction is Un { Op: MUnOp.Plus } unary)
        {
            return new Move(
                Dst: unary.Dst,
                Src: unary.X);
        }

        if (instruction is not Bin binary)
        {
            return instruction;
        }

        return binary.Op switch
        {
            MBinOp.Add when IsZero(binary.R) => new Move(
                Dst: binary.Dst,
                Src: binary.L),
            MBinOp.Add when IsZero(binary.L) => new Move(
                Dst: binary.Dst,
                Src: binary.R),
            MBinOp.Sub when IsZero(binary.R) => new Move(
                Dst: binary.Dst,
                Src: binary.L),
            MBinOp.Mul when IsOne(binary.R) => new Move(
                Dst: binary.Dst,
                Src: binary.L),
            MBinOp.Mul when IsOne(binary.L) => new Move(
                Dst: binary.Dst,
                Src: binary.R),
            MBinOp.Mul when IsZero(binary.R) => new Move(
                Dst: binary.Dst,
                Src: new Const(0L)),
            MBinOp.Mul when IsZero(binary.L) => new Move(
                Dst: binary.Dst,
                Src: new Const(0L)),
            MBinOp.Div when IsOne(binary.R) => new Move(
                Dst: binary.Dst,
                Src: binary.L),
            MBinOp.Mod when IsOne(binary.R) => new Move(
                Dst: binary.Dst,
                Src: new Const(0L)),
            _ => instruction
        };
    }
}
