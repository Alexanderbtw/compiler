using Compiler.Frontend.Translation.MIR.Instructions.Abstractions;

namespace Compiler.Frontend.Translation.MIR.Optimization.Infrastructure;

public static class MirConstantEvaluator
{
    public static bool TryEvaluateBinary(
        MBinOp op,
        object? leftValue,
        object? rightValue,
        out object? result)
    {
        result = null;

        switch (op)
        {
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
                if (leftValue is long mulL && rightValue is long mulR)
                {
                    result = mulL * mulR;

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
            case MBinOp.Eq:
                if (IsSupportedComparison(
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
                if (IsSupportedComparison(
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

    public static bool TryEvaluateBuiltinCall(
        string callee,
        IReadOnlyList<object?> args,
        out object? result)
    {
        result = null;

        switch (callee)
        {
            case "len" when args.Count == 1:
                if (args[0] is string s)
                {
                    result = (long)s.Length;

                    return true;
                }

                return false;
            case "ord" when args.Count == 1:
                if (args[0] is char ch)
                {
                    result = (long)ch;

                    return true;
                }

                if (args[0] is string
                    {
                        Length: 1
                    } single)
                {
                    result = (long)single[0];

                    return true;
                }

                return false;
            default:
                return false;
        }
    }

    public static bool TryEvaluateUnary(
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
                    result = plus;

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

    private static bool IsSupportedComparison(
        object? left,
        object? right)
    {
        if (left is null || right is null)
        {
            return true;
        }

        Type leftType = left.GetType();
        Type rightType = right.GetType();

        return leftType == rightType &&
            (leftType == typeof(long) || leftType == typeof(bool) || leftType == typeof(char) || leftType == typeof(string));
    }
}
