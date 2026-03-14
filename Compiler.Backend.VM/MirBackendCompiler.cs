using Compiler.Backend.JIT.Abstractions;
using Compiler.Frontend.Translation.MIR.Common;
using Compiler.Frontend.Translation.MIR.Instructions;
using Compiler.Frontend.Translation.MIR.Instructions.Abstractions;
using Compiler.Frontend.Translation.MIR.Operands;
using Compiler.Frontend.Translation.MIR.Operands.Abstractions;
using Compiler.Runtime.VM.Bytecode;

namespace Compiler.Backend.VM;

/// <summary>
///     Default backend compiler: lowers MIR into register bytecode for the VM.
/// </summary>
public sealed class MirBackendCompiler : IBackendCompiler<VmCompiledProgram>
{
    public VmCompiledProgram Compile(
        MirModule mirModule)
    {
        var functions = new Dictionary<string, VmFunction>(
            capacity: mirModule.Functions.Count,
            comparer: StringComparer.Ordinal);

        foreach (MirFunction function in mirModule.Functions)
        {
            functions[function.Name] = CompileFunction(function);
        }

        return new VmCompiledProgram(new VmProgram(functions));
    }

    private static VmFunction CompileFunction(
        MirFunction function)
    {
        var constants = new List<VmConstant>();
        var constantMap = new Dictionary<object, int>();
        IReadOnlyDictionary<MirBlock, int> blockOffsets = ComputeBlockOffsets(function);
        var instructions = new List<VmInstruction>(blockOffsets.Values.LastOrDefault() + 1);

        foreach (MirBlock block in function.Blocks)
        {
            foreach (MirInstr instruction in block.Instructions)
            {
                instructions.Add(
                    CompileInstruction(
                        instruction: instruction,
                        blockOffsets: blockOffsets,
                        constants: constants,
                        constantMap: constantMap));
            }

            if (block.Terminator is not null)
            {
                instructions.Add(
                    CompileInstruction(
                        instruction: block.Terminator,
                        blockOffsets: blockOffsets,
                        constants: constants,
                        constantMap: constantMap));
            }
        }

        return new VmFunction(
            name: function.Name,
            registerCount: ComputeRegisterCount(function),
            parameterCount: function.ParamRegs.Count,
            parameterRegisters: function
                .ParamRegs
                .Select(param => param.Id)
                .ToArray(),
            instructions: instructions,
            constants: constants);
    }

    private static VmInstruction CompileInstruction(
        MirInstr instruction,
        IReadOnlyDictionary<MirBlock, int> blockOffsets,
        List<VmConstant> constants,
        Dictionary<object, int> constantMap)
    {
        return instruction switch
        {
            Move move => new VmMoveInstruction(
                DestinationRegister: move.Dst.Id,
                Source: CompileOperand(
                    operand: move.Src,
                    constants: constants,
                    constantMap: constantMap)),
            Bin binary => new VmBinaryInstruction(
                DestinationRegister: binary.Dst.Id,
                Operation: binary.Op,
                Left: CompileOperand(
                    operand: binary.L,
                    constants: constants,
                    constantMap: constantMap),
                Right: CompileOperand(
                    operand: binary.R,
                    constants: constants,
                    constantMap: constantMap)),
            Un unary => new VmUnaryInstruction(
                DestinationRegister: unary.Dst.Id,
                Operation: unary.Op,
                Operand: CompileOperand(
                    operand: unary.X,
                    constants: constants,
                    constantMap: constantMap)),
            LoadIndex loadIndex => new VmLoadIndexInstruction(
                DestinationRegister: loadIndex.Dst.Id,
                ArrayOperand: CompileOperand(
                    operand: loadIndex.Arr,
                    constants: constants,
                    constantMap: constantMap),
                IndexOperand: CompileOperand(
                    operand: loadIndex.Index,
                    constants: constants,
                    constantMap: constantMap)),
            StoreIndex storeIndex => new VmStoreIndexInstruction(
                ArrayOperand: CompileOperand(
                    operand: storeIndex.Arr,
                    constants: constants,
                    constantMap: constantMap),
                IndexOperand: CompileOperand(
                    operand: storeIndex.Index,
                    constants: constants,
                    constantMap: constantMap),
                ValueOperand: CompileOperand(
                    operand: storeIndex.Value,
                    constants: constants,
                    constantMap: constantMap)),
            Call call => new VmCallInstruction(
                DestinationRegister: call.Dst?.Id,
                Callee: call.Callee,
                Arguments: call
                    .Args
                    .Select(arg => CompileOperand(
                        operand: arg,
                        constants: constants,
                        constantMap: constantMap))
                    .ToArray()),
            Br branch => new VmBranchInstruction(blockOffsets[branch.Target]),
            BrCond branchCondition => new VmBranchConditionInstruction(
                Condition: CompileOperand(
                    operand: branchCondition.Cond,
                    constants: constants,
                    constantMap: constantMap),
                TrueTarget: blockOffsets[branchCondition.IfTrue],
                FalseTarget: blockOffsets[branchCondition.IfFalse]),
            Ret ret => new VmReturnInstruction(
                Value: ret.Value is null
                    ? null
                    : CompileOperand(
                        operand: ret.Value,
                        constants: constants,
                        constantMap: constantMap)),
            Phi => throw new NotSupportedException("phi instructions are not supported in VM bytecode"),
            _ => throw new NotSupportedException(
                instruction.GetType()
                    .Name)
        };
    }

    private static VmOperand CompileOperand(
        MOperand operand,
        List<VmConstant> constants,
        Dictionary<object, int> constantMap)
    {
        return operand switch
        {
            VReg register => VmOperand.Register(register.Id),
            Const constant => VmOperand.Constant(
                GetOrAddConstant(
                    value: constant.Value,
                    constants: constants,
                    constantMap: constantMap)),
            _ => throw new NotSupportedException(
                operand.GetType()
                    .Name)
        };
    }

    private static IReadOnlyDictionary<MirBlock, int> ComputeBlockOffsets(
        MirFunction function)
    {
        var offsets = new Dictionary<MirBlock, int>();
        var nextOffset = 0;

        foreach (MirBlock block in function.Blocks)
        {
            offsets[block] = nextOffset;
            nextOffset += block.Instructions.Count + (block.Terminator is null
                ? 0
                : 1);
        }

        return offsets;
    }

    private static int ComputeRegisterCount(
        MirFunction function)
    {
        var maxRegisterId = 0;

        foreach (VReg register in function.ParamRegs)
        {
            maxRegisterId = Math.Max(
                val1: maxRegisterId,
                val2: register.Id);
        }

        foreach (MirBlock block in function.Blocks)
        {
            foreach (MirInstr instruction in block.Instructions)
            {
                maxRegisterId = Math.Max(
                    val1: maxRegisterId,
                    val2: GetMaxRegisterId(instruction));
            }

            if (block.Terminator is not null)
            {
                maxRegisterId = Math.Max(
                    val1: maxRegisterId,
                    val2: GetMaxRegisterId(block.Terminator));
            }
        }

        return Math.Max(
            val1: 1,
            val2: maxRegisterId + 1);
    }

    private static VmConstant CreateConstant(
        object? value)
    {
        return value switch
        {
            null => VmConstant.Null(),
            long longValue => VmConstant.FromLong(longValue),
            bool boolValue => VmConstant.FromBool(boolValue),
            char charValue => VmConstant.FromChar(charValue),
            string stringValue => VmConstant.FromString(stringValue),
            _ => throw new NotSupportedException($"const {value.GetType().Name}")
        };
    }

    private static int GetMaxRegisterId(
        MirInstr instruction)
    {
        var maxRegisterId = 0;

        void ConsiderOperand(
            MOperand operand)
        {
            if (operand is VReg register)
            {
                maxRegisterId = Math.Max(
                    val1: maxRegisterId,
                    val2: register.Id);
            }
        }

        switch (instruction)
        {
            case Move move:
                maxRegisterId = Math.Max(
                    val1: maxRegisterId,
                    val2: move.Dst.Id);

                ConsiderOperand(move.Src);

                break;
            case Bin binary:
                maxRegisterId = Math.Max(
                    val1: maxRegisterId,
                    val2: binary.Dst.Id);

                ConsiderOperand(binary.L);
                ConsiderOperand(binary.R);

                break;
            case Un unary:
                maxRegisterId = Math.Max(
                    val1: maxRegisterId,
                    val2: unary.Dst.Id);

                ConsiderOperand(unary.X);

                break;
            case LoadIndex loadIndex:
                maxRegisterId = Math.Max(
                    val1: maxRegisterId,
                    val2: loadIndex.Dst.Id);

                ConsiderOperand(loadIndex.Arr);
                ConsiderOperand(loadIndex.Index);

                break;
            case StoreIndex storeIndex:
                ConsiderOperand(storeIndex.Arr);
                ConsiderOperand(storeIndex.Index);
                ConsiderOperand(storeIndex.Value);

                break;
            case Call call:
                if (call.Dst is { } callDst)
                {
                    maxRegisterId = Math.Max(
                        val1: maxRegisterId,
                        val2: callDst.Id);
                }

                foreach (MOperand argument in call.Args)
                {
                    ConsiderOperand(argument);
                }

                break;
            case BrCond branchCondition:
                ConsiderOperand(branchCondition.Cond);

                break;
            case Ret { Value: { } returnValue }:
                ConsiderOperand(returnValue);

                break;
        }

        return maxRegisterId;
    }

    private static int GetOrAddConstant(
        object? value,
        List<VmConstant> constants,
        Dictionary<object, int> constantMap)
    {
        object key = value ?? NullConstantKey.Instance;

        if (constantMap.TryGetValue(
                key: key,
                value: out int index))
        {
            return index;
        }

        index = constants.Count;
        constants.Add(CreateConstant(value));
        constantMap[key] = index;

        return index;
    }

    private sealed class NullConstantKey
    {
        public static readonly NullConstantKey Instance = new NullConstantKey();
    }
}
