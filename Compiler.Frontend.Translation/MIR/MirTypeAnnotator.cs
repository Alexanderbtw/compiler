using Compiler.Frontend.Translation.MIR.Common;
using Compiler.Frontend.Translation.MIR.Instructions;
using Compiler.Frontend.Translation.MIR.Instructions.Abstractions;
using Compiler.Frontend.Translation.MIR.Operands;
using Compiler.Frontend.Translation.MIR.Operands.Abstractions;

namespace Compiler.Frontend.Translation.MIR;

/// <summary>
///     Minimal type inference pass for MIR to help IL generation TODO: Not ready yet
/// </summary>
public sealed class MirTypeAnnotator
{
    public void Annotate(
        MirModule mirModule)
    {
        foreach (MirFunction function in mirModule.Functions)
        {
            Annotate(function);
        }
    }

    private static void Annotate(
        MirFunction function)
    {
        // Function parameters: default to Obj (can be refined later)
        foreach (VReg parameterRegister in function.ParamRegs)
        {
            SetTypeIfEmpty(
                function: function,
                register: parameterRegister,
                type: MirFunction.MType.Obj);
        }

        bool hasChanged;
        int iterationCount = 0;
        const int maxIterations = 50; // safety guard to avoid infinite loops

        do
        {
            hasChanged = false;
            iterationCount++;

            if (iterationCount > maxIterations)
            {
                break;
            }

            foreach (MirBlock block in function.Blocks)
            {
                foreach (MirInstr instruction in block.Instructions)
                {
                    switch (instruction)
                    {
                        case Move move:
                            hasChanged |= MergeType(
                                function: function,
                                destination: move.Dst,
                                targetType: GetOperandType(
                                    function: function,
                                    operand: move.Src));

                            break;

                        case Bin binary:
                            if (IsI64ArithmeticBinaryOp(binary.Op))
                            {
                                hasChanged |= MergeType(
                                    function: function,
                                    destination: binary.Dst,
                                    targetType: MirFunction.MType.I64);
                            }
                            else if (IsComparisonBinaryOp(binary.Op))
                            {
                                hasChanged |= MergeType(
                                    function: function,
                                    destination: binary.Dst,
                                    targetType: MirFunction.MType.Bool);
                            }
                            else
                            {
                                hasChanged |= MergeType(
                                    function: function,
                                    destination: binary.Dst,
                                    targetType: MirFunction.MType.Obj);
                            }

                            break;

                        case Un unary:
                            if (unary.Op is MUnOp.Neg or MUnOp.Plus)
                            {
                                hasChanged |= MergeType(
                                    function: function,
                                    destination: unary.Dst,
                                    targetType: MirFunction.MType.I64);
                            }
                            else if (unary.Op is MUnOp.Not)
                            {
                                hasChanged |= MergeType(
                                    function: function,
                                    destination: unary.Dst,
                                    targetType: MirFunction.MType.Bool);
                            }
                            else
                            {
                                hasChanged |= MergeType(
                                    function: function,
                                    destination: unary.Dst,
                                    targetType: MirFunction.MType.Obj);
                            }

                            break;

                        case LoadIndex loadIndex:
                            // Unknown element type at this pass → Obj
                            hasChanged |= MergeType(
                                function: function,
                                destination: loadIndex.Dst,
                                targetType: MirFunction.MType.Obj);

                            break;

                        case Call call:
                            hasChanged |= MergeType(
                                function: function,
                                destination: call.Dst,
                                targetType: GuessCalleeReturnType(call.Callee));

                            break;
                    }
                }
            }
        }
        while (hasChanged);
    }

    private static MirFunction.MType GetOperandType(
        MirFunction function,
        MOperand? operand)
    {
        return operand switch
        {
            VReg vreg => function.Types.GetValueOrDefault(
                key: vreg.Id,
                defaultValue: MirFunction.MType.Obj),
            Const constant => constant.Value switch
            {
                long => MirFunction.MType.I64,
                bool => MirFunction.MType.Bool,
                char => MirFunction.MType.Char,
                _ => MirFunction.MType.Obj
            },
            _ => MirFunction.MType.Obj
        };
    }

    private static MirFunction.MType GuessCalleeReturnType(
        string calleeName)
    {
        return calleeName switch
        {
            "len" => MirFunction.MType.I64,
            "ord" => MirFunction.MType.I64,
            "clock_ms" => MirFunction.MType.I64,
            "chr" => MirFunction.MType.Char,
            "print" => MirFunction.MType.Obj,
            "assert" => MirFunction.MType.Obj,
            _ => MirFunction.MType.Obj
        };
    }

    private static bool IsComparisonBinaryOp(
        MBinOp op)
    {
        return op is MBinOp.Lt or MBinOp.Le or MBinOp.Gt or MBinOp.Ge or MBinOp.Eq or MBinOp.Ne;
    }

    private static bool IsI64ArithmeticBinaryOp(
        MBinOp op)
    {
        return op is MBinOp.Add or MBinOp.Sub or MBinOp.Mul or MBinOp.Div or MBinOp.Mod;
    }

    private static bool MergeType(
        MirFunction function,
        VReg? destination,
        MirFunction.MType targetType)
    {
        if (destination is null)
        {
            return false;
        }

        if (!function.Types.TryGetValue(
                key: destination.Id,
                value: out MirFunction.MType currentType))
        {
            function.Types[destination.Id] = targetType;

            return true;
        }

        if (currentType == targetType)
        {
            return false;
        }

        // Simplified widening: any conflict widens to Obj
        MirFunction.MType widened =
            currentType == MirFunction.MType.Obj || targetType == MirFunction.MType.Obj
                ? MirFunction.MType.Obj
                : currentType == targetType
                    ? currentType
                    : MirFunction.MType.Obj;

        if (widened != currentType)
        {
            function.Types[destination.Id] = widened;

            return true;
        }

        return false;
    }

    private static void SetTypeIfEmpty(
        MirFunction function,
        VReg register,
        MirFunction.MType type)
    {
        function.Types.TryAdd(
            key: register.Id,
            value: type);
    }
}
