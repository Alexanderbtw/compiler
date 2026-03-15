using Compiler.Frontend.Translation.MIR.Common;
using Compiler.Frontend.Translation.MIR.Instructions;
using Compiler.Frontend.Translation.MIR.Instructions.Abstractions;
using Compiler.Frontend.Translation.MIR.Operands;
using Compiler.Frontend.Translation.MIR.Operands.Abstractions;
using Compiler.Frontend.Translation.MIR.Optimization.Infrastructure;

namespace Compiler.Frontend.Translation.MIR.Optimization.Passes;

public sealed class LocalCanonicalizationPass : IMirOptimizationPass
{
    public string Name => nameof(LocalCanonicalizationPass);

    public MirPassResult Run(
        MirFunction function,
        MirAnalysisManager analyses)
    {
        var changed = false;

        foreach (MirBlock block in function.Blocks)
        {
            var environment = new Dictionary<int, MOperand>();
            var rewrittenInstructions = new List<MirInstr>(block.Instructions.Count);

            foreach (MirInstr instruction in block.Instructions)
            {
                switch (instruction)
                {
                    case Move move:
                        {
                            MOperand source = ResolveOperand(
                                environment: environment,
                                operand: move.Src);

                            environment[move.Dst.Id] = source;

                            if (source is VReg sourceRegister && sourceRegister.Id == move.Dst.Id)
                            {
                                changed = true;

                                break;
                            }

                            rewrittenInstructions.Add(
                                new Move(
                                    Dst: move.Dst,
                                    Src: source));

                            changed |= source != move.Src;

                            break;
                        }
                    case Bin binary:
                        {
                            MOperand left = ResolveOperand(
                                environment: environment,
                                operand: binary.L);

                            MOperand right = ResolveOperand(
                                environment: environment,
                                operand: binary.R);

                            if (left is Const leftConst && right is Const rightConst &&
                                MirConstantEvaluator.TryEvaluateBinary(
                                    op: binary.Op,
                                    leftValue: leftConst.Value,
                                    rightValue: rightConst.Value,
                                    result: out object? folded))
                            {
                                var constant = new Const(folded);
                                environment[binary.Dst.Id] = constant;
                                rewrittenInstructions.Add(
                                    new Move(
                                        Dst: binary.Dst,
                                        Src: constant));

                                changed = true;
                            }
                            else
                            {
                                rewrittenInstructions.Add(
                                    new Bin(
                                        Dst: binary.Dst,
                                        Op: binary.Op,
                                        L: left,
                                        R: right));

                                environment.Remove(binary.Dst.Id);
                                changed |= left != binary.L || right != binary.R;
                            }

                            break;
                        }
                    case Un unary:
                        {
                            MOperand operand = ResolveOperand(
                                environment: environment,
                                operand: unary.X);

                            if (operand is Const constOperand &&
                                MirConstantEvaluator.TryEvaluateUnary(
                                    op: unary.Op,
                                    value: constOperand.Value,
                                    result: out object? folded))
                            {
                                var constant = new Const(folded);
                                environment[unary.Dst.Id] = constant;
                                rewrittenInstructions.Add(
                                    new Move(
                                        Dst: unary.Dst,
                                        Src: constant));

                                changed = true;
                            }
                            else
                            {
                                rewrittenInstructions.Add(
                                    new Un(
                                        Dst: unary.Dst,
                                        Op: unary.Op,
                                        X: operand));

                                environment.Remove(unary.Dst.Id);
                                changed |= operand != unary.X;
                            }

                            break;
                        }
                    case LoadIndex loadIndex:
                        {
                            MOperand arrayOperand = ResolveOperand(
                                environment: environment,
                                operand: loadIndex.Arr);

                            MOperand indexOperand = ResolveOperand(
                                environment: environment,
                                operand: loadIndex.Index);

                            environment.Remove(loadIndex.Dst.Id);
                            rewrittenInstructions.Add(
                                new LoadIndex(
                                    Dst: loadIndex.Dst,
                                    Arr: arrayOperand,
                                    Index: indexOperand));

                            changed |= arrayOperand != loadIndex.Arr || indexOperand != loadIndex.Index;

                            break;
                        }
                    case StoreIndex storeIndex:
                        {
                            MOperand arrayOperand = ResolveOperand(
                                environment: environment,
                                operand: storeIndex.Arr);

                            MOperand indexOperand = ResolveOperand(
                                environment: environment,
                                operand: storeIndex.Index);

                            MOperand valueOperand = ResolveOperand(
                                environment: environment,
                                operand: storeIndex.Value);

                            rewrittenInstructions.Add(
                                new StoreIndex(
                                    Arr: arrayOperand,
                                    Index: indexOperand,
                                    Value: valueOperand));

                            changed |= arrayOperand != storeIndex.Arr ||
                                indexOperand != storeIndex.Index ||
                                valueOperand != storeIndex.Value;

                            break;
                        }
                    case Call call:
                        {
                            MOperand[] rewrittenArgs = call
                                .Args
                                .Select(arg => ResolveOperand(
                                    environment: environment,
                                    operand: arg))
                                .ToArray();

                            if (call.Dst is not null)
                            {
                                environment.Remove(call.Dst.Id);
                            }

                            rewrittenInstructions.Add(
                                new Call(
                                    Dst: call.Dst,
                                    Callee: call.Callee,
                                    Args: rewrittenArgs));

                            changed |= !rewrittenArgs.SequenceEqual(call.Args);

                            break;
                        }
                    default:
                        rewrittenInstructions.Add(instruction);
                        environment.Clear();

                        break;
                }
            }

            block.Instructions.Clear();
            block.Instructions.AddRange(rewrittenInstructions);

            if (block.Terminator is not null)
            {
                MirInstr rewrittenTerminator = MirInstructionUtilities.ReplaceOperands(
                    instruction: block.Terminator,
                    rewriteOperand: operand => ResolveFromRegisterOnly(
                        environment: environment,
                        operand: operand));

                changed |= rewrittenTerminator != block.Terminator;
                block.Terminator = rewrittenTerminator;
            }
        }

        return changed
            ? MirPassResult.ChangedAnalyses(MirAnalysisKind.ConstantState | MirAnalysisKind.Liveness)
            : MirPassResult.NoChange;
    }

    private static MOperand ResolveFromRegisterOnly(
        Dictionary<int, MOperand> environment,
        MOperand operand)
    {
        return operand is VReg register && environment.TryGetValue(
            key: register.Id,
            value: out MOperand? mapped)
            ? mapped
            : operand;
    }

    private static MOperand ResolveOperand(
        Dictionary<int, MOperand> environment,
        MOperand operand)
    {
        return operand is VReg register && environment.TryGetValue(
            key: register.Id,
            value: out MOperand? mapped)
            ? mapped
            : operand;
    }
}
