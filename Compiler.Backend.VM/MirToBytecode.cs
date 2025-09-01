using Compiler.Frontend.Translation.MIR.Common;
using Compiler.Frontend.Translation.MIR.Instructions;
using Compiler.Frontend.Translation.MIR.Instructions.Abstractions;
using Compiler.Frontend.Translation.MIR.Operands;

namespace Compiler.Backend.VM;

/// <summary>
///     Lowers MIR (Mid-level IR) into a simple stack-based bytecode for our VM.
///     Focuses on readability: descriptive identifiers and minimal abbreviations.
/// </summary>
public sealed class MirToBytecode
{
    /// <summary>
    ///     Entry point: lower an entire MIR module into a VM module.
    /// </summary>
    public VmModule Lower(
        MirModule mirModule)
    {
        var vmModule = new VmModule();

        // Сначала регистрируем все функции, чтобы заранее знать их индексы
        foreach (MirFunction mirFunction in mirModule.Functions)
        {
            vmModule.AddFunction(
                new VmFunction(
                    name: mirFunction.Name,
                    arity: mirFunction.ParamRegs.Count));
        }

        // Затем понижаем каждую функцию по индексу (соответствие MIR↔VM сохраняется)
        for (int functionIndex = 0; functionIndex < mirModule.Functions.Count; functionIndex++)
        {
            LowerFunction(
                mirFunction: mirModule.Functions[functionIndex],
                vmFunction: vmModule.Functions[functionIndex],
                vmModule: vmModule);
        }

        return vmModule;
    }

    private static void LowerFunction(
        MirFunction mirFunction,
        VmFunction vmFunction,
        VmModule vmModule)
    {
        // Отображение виртуальных регистров MIR в локальные индексы VM
        var virtualRegisterToLocalIndex = new Dictionary<int, int>();

        int AllocateNextLocalIndex() =>
            virtualRegisterToLocalIndex.Count;

        // Параметры функции → локальные слоты (в порядке ParamRegs)
        foreach (VReg parameterRegister in mirFunction.ParamRegs)
        {
            if (!virtualRegisterToLocalIndex.ContainsKey(parameterRegister.Id))
            {
                virtualRegisterToLocalIndex[parameterRegister.Id] = AllocateNextLocalIndex();
            }

            vmFunction.ParamLocalIndices.Add(virtualRegisterToLocalIndex[parameterRegister.Id]);
        }

        List<Instr> instructions = vmFunction.Code;
        var blockToInstructionIndex = new Dictionary<MirBlock, int>();
        var pendingUnconditionalBranchPatches = new List<(int instructionIndex, MirBlock targetBlock)>();
        var pendingConditionalBranchPatches = new List<(int truePatchIndex, MirBlock trueBlock, int falsePatchIndex, MirBlock falseBlock)>();

        // Локальные помощники загрузки/сохранения операндов
        void EmitLoadOperand(
            MOperand operand)
        {
            switch (operand)
            {
                case Const constant:
                    switch (constant.Value)
                    {
                        case null:
                            instructions.Add(new Instr { Op = OpCode.LdcNull });

                            break;
                        case long number:
                            instructions.Add(new Instr { Op = OpCode.LdcI64, Imm = number });

                            break;
                        case bool booleanValue:
                            instructions.Add(
                                new Instr
                                {
                                    Op = OpCode.LdcBool, Imm = booleanValue
                                        ? 1
                                        : 0
                                });

                            break;
                        case char character:
                            instructions.Add(new Instr { Op = OpCode.LdcChar, Imm = character });

                            break;
                        case string text:
                            int stringPoolIndex = vmModule.AddString(text);
                            instructions.Add(new Instr { Op = OpCode.LdcStr, Idx = stringPoolIndex });

                            break;
                        default:
                            throw new NotSupportedException($"const {constant.Value?.GetType().Name}");
                    }

                    break;

                case VReg virtualRegister:
                    if (!virtualRegisterToLocalIndex.TryGetValue(
                            key: virtualRegister.Id,
                            value: out int localIndex))
                    {
                        localIndex = AllocateNextLocalIndex();
                        virtualRegisterToLocalIndex[virtualRegister.Id] = localIndex;
                    }

                    instructions.Add(new Instr { Op = OpCode.LdLoc, A = localIndex });

                    break;

                default:
                    throw new NotSupportedException(
                        operand.GetType()
                            .Name);
            }
        }

        void EmitStoreToDestination(
            VReg destination)
        {
            if (!virtualRegisterToLocalIndex.TryGetValue(
                    key: destination.Id,
                    value: out int localIndex))
            {
                localIndex = AllocateNextLocalIndex();
                virtualRegisterToLocalIndex[destination.Id] = localIndex;
            }

            instructions.Add(new Instr { Op = OpCode.StLoc, A = localIndex });
        }

        // Проход по блокам в порядке объявления
        for (int blockIndex = 0; blockIndex < mirFunction.Blocks.Count; blockIndex++)
        {
            MirBlock block = mirFunction.Blocks[blockIndex];
            bool isLastBlock = blockIndex == mirFunction.Blocks.Count - 1;
            blockToInstructionIndex[block] = instructions.Count;

            foreach (MirInstr instruction in block.Instructions)
            {
                switch (instruction)
                {
                    case Move move:
                        EmitLoadOperand(move.Src);
                        EmitStoreToDestination(move.Dst);

                        break;

                    case Bin binary:
                        EmitLoadOperand(binary.L);
                        EmitLoadOperand(binary.R);
                        instructions.Add(
                            new Instr
                            {
                                Op = binary.Op switch
                                {
                                    MBinOp.Add => OpCode.Add,
                                    MBinOp.Sub => OpCode.Sub,
                                    MBinOp.Mul => OpCode.Mul,
                                    MBinOp.Div => OpCode.Div,
                                    MBinOp.Mod => OpCode.Mod,
                                    MBinOp.Lt => OpCode.Lt,
                                    MBinOp.Le => OpCode.Le,
                                    MBinOp.Gt => OpCode.Gt,
                                    MBinOp.Ge => OpCode.Ge,
                                    MBinOp.Eq => OpCode.Eq,
                                    MBinOp.Ne => OpCode.Ne,
                                    _ => throw new NotSupportedException(binary.Op.ToString())
                                }
                            });

                        EmitStoreToDestination(binary.Dst);

                        break;

                    case Un unary:
                        EmitLoadOperand(unary.X);

                        switch (unary.Op)
                        {
                            case MUnOp.Neg:
                                instructions.Add(new Instr { Op = OpCode.Neg });

                                break;
                            case MUnOp.Plus:
                                // унарный плюс — это no-op
                                break;
                            case MUnOp.Not:
                                instructions.Add(new Instr { Op = OpCode.Not });

                                break;
                            default:
                                throw new NotSupportedException(unary.Op.ToString());
                        }

                        EmitStoreToDestination(unary.Dst);

                        break;

                    case LoadIndex loadIndex:
                        EmitLoadOperand(loadIndex.Arr);
                        EmitLoadOperand(loadIndex.Index);
                        instructions.Add(new Instr { Op = OpCode.LdElem });
                        EmitStoreToDestination(loadIndex.Dst);

                        break;

                    case StoreIndex storeIndex:
                        EmitLoadOperand(storeIndex.Arr);
                        EmitLoadOperand(storeIndex.Index);
                        EmitLoadOperand(storeIndex.Value);
                        instructions.Add(new Instr { Op = OpCode.StElem });

                        break;

                    case Call call:
                        {
                            // загрузить аргументы слева направо (на стек)
                            foreach (MOperand arg in call.Args)
                            {
                                EmitLoadOperand(arg);
                            }

                            // Специализированные опкоды для горячих билтинов
                            if (call.Callee == "array" && call.Args.Count == 1)
                            {
                                instructions.Add(new Instr { Op = OpCode.NewArr });

                                if (call.Dst is null)
                                {
                                    instructions.Add(new Instr { Op = OpCode.Pop });
                                }
                                else
                                {
                                    EmitStoreToDestination(call.Dst);
                                }

                                break;
                            }

                            if (call.Callee == "len" && call.Args.Count == 1)
                            {
                                instructions.Add(new Instr { Op = OpCode.Len });

                                if (call.Dst is null)
                                {
                                    instructions.Add(new Instr { Op = OpCode.Pop });
                                }
                                else
                                {
                                    EmitStoreToDestination(call.Dst);
                                }

                                break;
                            }

                            if (vmModule.TryGetFunctionIndex(
                                    name: call.Callee,
                                    idx: out int calleeFunctionIndex))
                            {
                                instructions.Add(new Instr { Op = OpCode.CallUser, A = calleeFunctionIndex, B = call.Args.Count });
                            }
                            else
                            {
                                int builtinNameStringIndex = vmModule.AddString(call.Callee);
                                instructions.Add(new Instr { Op = OpCode.CallBuiltin, A = builtinNameStringIndex, B = call.Args.Count });
                            }

                            if (call.Dst is null)
                            {
                                instructions.Add(new Instr { Op = OpCode.Pop });
                            }
                            else
                            {
                                EmitStoreToDestination(call.Dst);
                            }

                            break;
                        }

                    default:
                        throw new NotSupportedException(
                            instruction.GetType()
                                .Name);
                }
            }

            // Терминатор (допускаем fallthrough при null; у последнего блока неявный `ret null`)
            if (block.Terminator is null)
            {
                if (isLastBlock)
                {
                    instructions.Add(new Instr { Op = OpCode.LdcNull });
                    instructions.Add(new Instr { Op = OpCode.Ret });
                }

                // иначе просто падаем дальше на следующий блок по линейному порядку
            }
            else
            {
                switch (block.Terminator)
                {
                    case Ret retTerminator:
                        if (retTerminator.Value is null)
                        {
                            instructions.Add(new Instr { Op = OpCode.LdcNull });
                        }
                        else
                        {
                            EmitLoadOperand(retTerminator.Value);
                        }

                        instructions.Add(new Instr { Op = OpCode.Ret });

                        break;

                    case Br branchTerminator:
                        {
                            int branchPlaceholderIndex = instructions.Count;
                            instructions.Add(new Instr { Op = OpCode.Br, A = -1 });
                            pendingUnconditionalBranchPatches.Add((branchPlaceholderIndex, branchTerminator.Target));

                            break;
                        }

                    case BrCond condTerminator:
                        {
                            EmitLoadOperand(condTerminator.Cond);
                            int trueBranchPlaceholderIndex = instructions.Count;
                            instructions.Add(new Instr { Op = OpCode.BrTrue, A = -1 });
                            int falseBranchPlaceholderIndex = instructions.Count;
                            instructions.Add(new Instr { Op = OpCode.Br, A = -1 });
                            pendingConditionalBranchPatches.Add((trueBranchPlaceholderIndex, condTerminator.IfTrue, falseBranchPlaceholderIndex, condTerminator.IfFalse));

                            break;
                        }

                    default:
                        throw new NotSupportedException(
                            block.Terminator.GetType()
                                .Name);
                }
            }
        }

        // Починить адреса переходов после того, как все блоки получили адреса
        foreach ((int instructionIndex, MirBlock targetBlock) in pendingUnconditionalBranchPatches)
        {
            vmFunction.Code[instructionIndex] = vmFunction.Code[instructionIndex] with { A = blockToInstructionIndex[targetBlock] };
        }

        foreach ((int truePatchIndex, MirBlock trueBlock, int falsePatchIndex, MirBlock falseBlock) in pendingConditionalBranchPatches)
        {
            vmFunction.Code[truePatchIndex] = vmFunction.Code[truePatchIndex] with { A = blockToInstructionIndex[trueBlock] };
            vmFunction.Code[falsePatchIndex] = vmFunction.Code[falsePatchIndex] with { A = blockToInstructionIndex[falseBlock] };
        }

        vmFunction.NLocals = virtualRegisterToLocalIndex.Count;
    }
}
