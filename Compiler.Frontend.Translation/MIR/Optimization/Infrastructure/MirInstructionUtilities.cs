using Compiler.Frontend.Translation.MIR.Common;
using Compiler.Frontend.Translation.MIR.Instructions;
using Compiler.Frontend.Translation.MIR.Instructions.Abstractions;
using Compiler.Frontend.Translation.MIR.Operands;
using Compiler.Frontend.Translation.MIR.Operands.Abstractions;

namespace Compiler.Frontend.Translation.MIR.Optimization;

public static class MirInstructionUtilities
{
    public static IReadOnlyList<int> GetDefs(
        MirInstr instruction)
    {
        return instruction switch
        {
            Move move => [move.Dst.Id],
            Bin binary => [binary.Dst.Id],
            Un unary => [unary.Dst.Id],
            LoadIndex loadIndex => [loadIndex.Dst.Id],
            Call { Dst: not null } call => [call.Dst!.Id],
            Phi phi => [phi.Dst.Id],
            _ => Array.Empty<int>()
        };
    }

    public static IReadOnlyList<int> GetUses(
        MirInstr instruction)
    {
        return instruction switch
        {
            Move move => GetOperandUses(move.Src),
            Bin binary => [.. GetOperandUses(binary.L), .. GetOperandUses(binary.R)],
            Un unary => GetOperandUses(unary.X),
            LoadIndex loadIndex => [.. GetOperandUses(loadIndex.Arr), .. GetOperandUses(loadIndex.Index)],
            StoreIndex storeIndex => [.. GetOperandUses(storeIndex.Arr), .. GetOperandUses(storeIndex.Index), .. GetOperandUses(storeIndex.Value)],
            Call call => call.Args.SelectMany(GetOperandUses).ToArray(),
            BrCond branchCondition => GetOperandUses(branchCondition.Cond),
            Ret { Value: not null } ret => GetOperandUses(ret.Value!),
            Phi phi => phi.Incomings.SelectMany(i => GetOperandUses(i.value)).ToArray(),
            _ => Array.Empty<int>()
        };
    }

    public static bool IsDeletableInstruction(
        MirInstr instruction)
    {
        return instruction switch
        {
            Move => true,
            Un unary => unary.Op is MUnOp.Plus or MUnOp.Neg or MUnOp.Not,
            Bin binary => binary.Op is MBinOp.Add or MBinOp.Sub or MBinOp.Mul or MBinOp.Eq or MBinOp.Ne or MBinOp.Lt or MBinOp.Le or MBinOp.Gt or MBinOp.Ge,
            _ => false
        };
    }

    public static MirInstr ReplaceOperands(
        MirInstr instruction,
        Func<MOperand, MOperand> rewriteOperand)
    {
        return instruction switch
        {
            Move move => new Move(
                Dst: move.Dst,
                Src: rewriteOperand(move.Src)),
            Bin binary => new Bin(
                Dst: binary.Dst,
                Op: binary.Op,
                L: rewriteOperand(binary.L),
                R: rewriteOperand(binary.R)),
            Un unary => new Un(
                Dst: unary.Dst,
                Op: unary.Op,
                X: rewriteOperand(unary.X)),
            LoadIndex loadIndex => new LoadIndex(
                Dst: loadIndex.Dst,
                Arr: rewriteOperand(loadIndex.Arr),
                Index: rewriteOperand(loadIndex.Index)),
            StoreIndex storeIndex => new StoreIndex(
                Arr: rewriteOperand(storeIndex.Arr),
                Index: rewriteOperand(storeIndex.Index),
                Value: rewriteOperand(storeIndex.Value)),
            Call call => new Call(
                Dst: call.Dst,
                Callee: call.Callee,
                Args: call.Args.Select(rewriteOperand).ToArray()),
            BrCond branchCondition => new BrCond(
                Cond: rewriteOperand(branchCondition.Cond),
                IfTrue: branchCondition.IfTrue,
                IfFalse: branchCondition.IfFalse),
            Ret ret => new Ret(
                Value: ret.Value is null
                    ? null
                    : rewriteOperand(ret.Value)),
            Phi phi => new Phi(
                Dst: phi.Dst,
                Incomings: phi.Incomings
                    .Select(i => (i.block, rewriteOperand(i.value)))
                    .ToArray()),
            _ => instruction
        };
    }

    public static void ReplacePhiIncomingBlocks(
        MirFunction function,
        MirBlock from,
        MirBlock to)
    {
        foreach (MirBlock block in function.Blocks)
        {
            for (var i = 0; i < block.Instructions.Count; i++)
            {
                if (block.Instructions[i] is not Phi phi)
                {
                    continue;
                }

                IReadOnlyList<(MirBlock block, MOperand value)> incomings = phi.Incomings
                    .Select(incoming => incoming.block == from
                        ? (to, incoming.value)
                        : incoming)
                    .ToArray();

                block.Instructions[i] = new Phi(
                    Dst: phi.Dst,
                    Incomings: incomings);
            }
        }
    }

    public static void ReplaceSuccessorTargets(
        MirFunction function,
        Func<MirBlock, MirBlock> rewriteTarget)
    {
        foreach (MirBlock block in function.Blocks)
        {
            if (block.Terminator is Br branch)
            {
                MirBlock target = rewriteTarget(branch.Target);
                block.Terminator = ReferenceEquals(branch.Target, target)
                    ? branch
                    : new Br(target);
            }
            else if (block.Terminator is BrCond branchCondition)
            {
                MirBlock trueTarget = rewriteTarget(branchCondition.IfTrue);
                MirBlock falseTarget = rewriteTarget(branchCondition.IfFalse);

                if (!ReferenceEquals(branchCondition.IfTrue, trueTarget) ||
                    !ReferenceEquals(branchCondition.IfFalse, falseTarget))
                {
                    block.Terminator = new BrCond(
                        Cond: branchCondition.Cond,
                        IfTrue: trueTarget,
                        IfFalse: falseTarget);
                }
            }
        }
    }

    private static int[] GetOperandUses(
        MOperand operand)
    {
        return operand is VReg register
            ? [register.Id]
            : [];
    }
}
