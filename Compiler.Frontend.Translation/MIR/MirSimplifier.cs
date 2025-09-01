using Compiler.Frontend.Translation.MIR.Common;
using Compiler.Frontend.Translation.MIR.Instructions;
using Compiler.Frontend.Translation.MIR.Instructions.Abstractions;
using Compiler.Frontend.Translation.MIR.Operands;

namespace Compiler.Frontend.Translation.MIR;

/// <summary>
///     MIR simplification pass: constant folding, copy-propagation inside basic blocks,
///     and simple branch folding (BrCond with const bool).
///     Conservative by design (no reordering, no DCE beyond removing no-op moves).
/// </summary>
public sealed class MirSimplifier
{
    public void Run(
        MirModule mirModule)
    {
        foreach (MirFunction function in mirModule.Functions)
        {
            SimplifyFunction(function);
        }
    }

    public void SimplifyFunction(
        MirFunction function)
    {
        foreach (MirBlock basicBlock in function.Blocks)
        {
            // Environment maps vreg.Id -> MOperand (Const or VReg)
            var valueEnvironment = new Dictionary<int, MOperand>();
            var rewrittenInstructions = new List<MirInstr>(basicBlock.Instructions.Count);

            foreach (MirInstr instruction in basicBlock.Instructions)
            {
                switch (instruction)
                {
                    case Move moveInstruction:
                        {
                            MOperand source = ResolveOperand(
                                environment: valueEnvironment,
                                operand: moveInstruction.Src);

                            // Overwrite mapping for destination (it will now refer to the new source)
                            valueEnvironment[moveInstruction.Dst.Id] = source;

                            // Skip no-op move: v <- v
                            if (source is VReg sourceReg && sourceReg.Id == moveInstruction.Dst.Id)
                            {
                                break;
                            }

                            rewrittenInstructions.Add(
                                new Move(
                                    Dst: moveInstruction.Dst,
                                    Src: source));

                            break;
                        }

                    case Bin binaryInstruction:
                        {
                            MOperand left = ResolveOperand(
                                environment: valueEnvironment,
                                operand: binaryInstruction.L);

                            MOperand right = ResolveOperand(
                                environment: valueEnvironment,
                                operand: binaryInstruction.R);

                            if (left is Const leftConst && right is Const rightConst &&
                                TryEvaluateBinaryConst(
                                    op: binaryInstruction.Op,
                                    leftValue: leftConst.Value,
                                    rightValue: rightConst.Value,
                                    result: out object? folded))
                            {
                                var constant = new Const(folded);
                                valueEnvironment[binaryInstruction.Dst.Id] = constant;
                                rewrittenInstructions.Add(
                                    new Move(
                                        Dst: binaryInstruction.Dst,
                                        Src: constant));
                            }
                            else
                            {
                                // Use propagated operands
                                rewrittenInstructions.Add(
                                    new Bin(
                                        Dst: binaryInstruction.Dst,
                                        Op: binaryInstruction.Op,
                                        L: left,
                                        R: right));

                                // Destination now holds a fresh unknown value, forget previous binding
                                valueEnvironment.Remove(binaryInstruction.Dst.Id);
                            }

                            break;
                        }

                    case Un unaryInstruction:
                        {
                            MOperand operand = ResolveOperand(
                                environment: valueEnvironment,
                                operand: unaryInstruction.X);

                            if (operand is Const constOperand &&
                                TryEvaluateUnaryConst(
                                    op: unaryInstruction.Op,
                                    value: constOperand.Value,
                                    result: out object? folded))
                            {
                                var constant = new Const(folded);
                                valueEnvironment[unaryInstruction.Dst.Id] = constant;
                                rewrittenInstructions.Add(
                                    new Move(
                                        Dst: unaryInstruction.Dst,
                                        Src: constant));
                            }
                            else
                            {
                                rewrittenInstructions.Add(
                                    new Un(
                                        Dst: unaryInstruction.Dst,
                                        Op: unaryInstruction.Op,
                                        X: operand));

                                valueEnvironment.Remove(unaryInstruction.Dst.Id);
                            }

                            break;
                        }

                    case LoadIndex loadIndexInstruction:
                        {
                            MOperand arrayOperand = ResolveOperand(
                                environment: valueEnvironment,
                                operand: loadIndexInstruction.Arr);

                            MOperand indexOperand = ResolveOperand(
                                environment: valueEnvironment,
                                operand: loadIndexInstruction.Index);

                            rewrittenInstructions.Add(
                                new LoadIndex(
                                    Dst: loadIndexInstruction.Dst,
                                    Arr: arrayOperand,
                                    Index: indexOperand));

                            valueEnvironment.Remove(loadIndexInstruction.Dst.Id);

                            break;
                        }

                    case StoreIndex storeIndexInstruction:
                        {
                            MOperand arrayOperand = ResolveOperand(
                                environment: valueEnvironment,
                                operand: storeIndexInstruction.Arr);

                            MOperand indexOperand = ResolveOperand(
                                environment: valueEnvironment,
                                operand: storeIndexInstruction.Index);

                            MOperand valueOperand = ResolveOperand(
                                environment: valueEnvironment,
                                operand: storeIndexInstruction.Value);

                            rewrittenInstructions.Add(
                                new StoreIndex(
                                    Arr: arrayOperand,
                                    Index: indexOperand,
                                    Value: valueOperand));

                            break;
                        }

                    case Call callInstruction:
                        {
                            var resolvedArgs = new List<MOperand>(callInstruction.Args.Count);

                            foreach (MOperand argument in callInstruction.Args)
                            {
                                resolvedArgs.Add(
                                    ResolveOperand(
                                        environment: valueEnvironment,
                                        operand: argument));
                            }

                            rewrittenInstructions.Add(
                                new Call(
                                    Dst: callInstruction.Dst,
                                    Callee: callInstruction.Callee,
                                    Args: resolvedArgs));

                            if (callInstruction.Dst is not null)
                            {
                                valueEnvironment.Remove(callInstruction.Dst.Id);
                            }

                            break;
                        }

                    default:
                        rewrittenInstructions.Add(instruction);

                        break;
                }
            }

            basicBlock.Instructions.Clear();
            basicBlock.Instructions.AddRange(rewrittenInstructions);

            // Terminator simplification: BrCond with constant bool
            if (basicBlock.Terminator is BrCond brCond)
            {
                MOperand condition = ResolveFromRegisterOnly(
                    op: brCond.Cond,
                    environment: valueEnvironment);

                if (condition is Const constant && constant.Value is bool boolean)
                {
                    basicBlock.Terminator = new Br(
                        boolean
                            ? brCond.IfTrue
                            : brCond.IfFalse);
                }
            }
        }
    }

    private static bool IsSupportedConstantComparison(
        object? left,
        object? right)
    {
        if (left is null || right is null)
        {
            return true;
        }

        Type leftType = left.GetType();
        Type rightType = right.GetType();

        if (leftType != rightType)
        {
            return false;
        }

        return leftType == typeof(long) || leftType == typeof(bool) || leftType == typeof(char) || leftType == typeof(string);
    }

    // For terminators we only consider direct VReg -> Const mapping (no deep chains),
    // because side effects across blocks are not tracked here.
    private static MOperand ResolveFromRegisterOnly(
        MOperand op,
        Dictionary<int, MOperand> environment)
    {
        return op is VReg register && environment.TryGetValue(
            key: register.Id,
            value: out MOperand? mapped)
            ? mapped
            : op;
    }

    private static MOperand ResolveOperand(
        Dictionary<int, MOperand> environment,
        MOperand operand)
    {
        if (operand is VReg register && environment.TryGetValue(
                key: register.Id,
                value: out MOperand? mapped))
        {
            return mapped;
        }

        return operand;
    }

    private static bool TryEvaluateBinaryConst(
        MBinOp op,
        object? leftValue,
        object? rightValue,
        out object? result)
    {
        result = null;

        switch (op)
        {
            // integer arithmetic
            case MBinOp.Add:
                if (leftValue is long addL && rightValue is long addR)
                {
                    result = addL + addR;

                    return true;
                }

                return false;
            case MBinOp.Sub:
                if (leftValue is long subL && rightValue is long subR)
                {
                    result = subL - subR;

                    return true;
                }

                return false;
            case MBinOp.Mul:
                if (leftValue is long mulLft && rightValue is long mulRgt)
                {
                    result = mulLft * mulRgt;

                    return true;
                }

                return false;
            case MBinOp.Div:
                if (leftValue is long divL && rightValue is long divR && divR != 0)
                {
                    result = divL / divR;

                    return true;
                }

                return false;
            case MBinOp.Mod:
                if (leftValue is long modL && rightValue is long modR && modR != 0)
                {
                    result = modL % modR;

                    return true;
                }

                return false;

            // comparisons
            case MBinOp.Eq:
                if (IsSupportedConstantComparison(
                        left: leftValue,
                        right: rightValue))
                {
                    result = Equals(
                        objA: leftValue,
                        objB: rightValue);

                    return true;
                }

                return false;
            case MBinOp.Ne:
                if (IsSupportedConstantComparison(
                        left: leftValue,
                        right: rightValue))
                {
                    result = !Equals(
                        objA: leftValue,
                        objB: rightValue);

                    return true;
                }

                return false;
            case MBinOp.Lt:
                if (leftValue is long ltL && rightValue is long ltR)
                {
                    result = ltL < ltR;

                    return true;
                }

                return false;
            case MBinOp.Le:
                if (leftValue is long leL && rightValue is long leR)
                {
                    result = leL <= leR;

                    return true;
                }

                return false;
            case MBinOp.Gt:
                if (leftValue is long gtL && rightValue is long gtR)
                {
                    result = gtL > gtR;

                    return true;
                }

                return false;
            case MBinOp.Ge:
                if (leftValue is long geL && rightValue is long geR)
                {
                    result = geL >= geR;

                    return true;
                }

                return false;

            default:
                return false;
        }
    }

    private static bool TryEvaluateUnaryConst(
        MUnOp op,
        object? value,
        out object? result)
    {
        result = null;

        switch (op)
        {
            case MUnOp.Plus:
                if (value is long plus)
                {
                    result = +plus;

                    return true;
                }

                return false;
            case MUnOp.Neg:
                if (value is long neg)
                {
                    result = -neg;

                    return true;
                }

                return false;
            case MUnOp.Not:
                if (value is bool boolean)
                {
                    result = !boolean;

                    return true;
                }

                return false;
            default:
                return false;
        }
    }
}
