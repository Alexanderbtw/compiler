using Compiler.Frontend.Translation.MIR.Common;
using Compiler.Frontend.Translation.MIR.Instructions;
using Compiler.Frontend.Translation.MIR.Instructions.Abstractions;
using Compiler.Frontend.Translation.MIR.Operands;
using Compiler.Frontend.Translation.MIR.Operands.Abstractions;
using Compiler.Frontend.Translation.MIR.Optimization.Infrastructure;

namespace Compiler.Frontend.Translation.MIR.Optimization.Passes;

public sealed class LocalCopyPropagationPass : IMirOptimizationPass
{
    public string Name => nameof(LocalCopyPropagationPass);

    public MirPassResult Run(
        MirFunction function,
        MirAnalysisManager analyses)
    {
        var changed = false;

        foreach (MirBlock block in function.Blocks)
        {
            var environment = new Dictionary<int, MOperand>();

            for (var i = 0; i < block.Instructions.Count; i++)
            {
                MirInstr instruction = block.Instructions[i];
                MirInstr rewritten = RewriteInstruction(
                    instruction: instruction,
                    environment: environment,
                    changed: out bool rewrittenChanged);

                if (rewrittenChanged)
                {
                    block.Instructions[i] = rewritten;
                    changed = true;
                }
            }

            if (block.Terminator is not null)
            {
                MirInstr rewrittenTerminator = MirInstructionUtilities.ReplaceOperands(
                    instruction: block.Terminator,
                    rewriteOperand: operand => ResolveFromRegisterOnly(
                        environment: environment,
                        operand: operand));

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

    private static MirInstr RewriteInstruction(
        MirInstr instruction,
        Dictionary<int, MOperand> environment,
        out bool changed)
    {
        changed = false;

        switch (instruction)
        {
            case Move move:
                {
                    MOperand source = ResolveOperand(
                        environment: environment,
                        operand: move.Src);

                    environment[move.Dst.Id] = source;
                    changed = source != move.Src;

                    return new Move(
                        Dst: move.Dst,
                        Src: source);
                }
            case Bin binary:
                {
                    MOperand left = ResolveOperand(
                        environment: environment,
                        operand: binary.L);

                    MOperand right = ResolveOperand(
                        environment: environment,
                        operand: binary.R);

                    environment.Remove(binary.Dst.Id);
                    changed = left != binary.L || right != binary.R;

                    return new Bin(
                        Dst: binary.Dst,
                        Op: binary.Op,
                        L: left,
                        R: right);
                }
            case Un unary:
                {
                    MOperand operand = ResolveOperand(
                        environment: environment,
                        operand: unary.X);

                    environment.Remove(unary.Dst.Id);
                    changed = operand != unary.X;

                    return new Un(
                        Dst: unary.Dst,
                        Op: unary.Op,
                        X: operand);
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
                    changed = arrayOperand != loadIndex.Arr || indexOperand != loadIndex.Index;

                    return new LoadIndex(
                        Dst: loadIndex.Dst,
                        Arr: arrayOperand,
                        Index: indexOperand);
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

                    changed = arrayOperand != storeIndex.Arr ||
                        indexOperand != storeIndex.Index ||
                        valueOperand != storeIndex.Value;

                    return new StoreIndex(
                        Arr: arrayOperand,
                        Index: indexOperand,
                        Value: valueOperand);
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

                    changed = !rewrittenArgs.SequenceEqual(call.Args);

                    return new Call(
                        Dst: call.Dst,
                        Callee: call.Callee,
                        Args: rewrittenArgs);
                }
            default:
                environment.Clear();

                return instruction;
        }
    }
}
