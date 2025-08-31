using Compiler.Frontend.Translation.MIR.Common;
using Compiler.Frontend.Translation.MIR.Instructions;
using Compiler.Frontend.Translation.MIR.Instructions.Abstractions;
using Compiler.Frontend.Translation.MIR.Operands;

namespace Compiler.Backend.VM;

public sealed class MirToBytecode
{
    public VmModule Lower(
        MirModule m)
    {
        var vm = new VmModule();

        // заранее зарегистрируем все функции, чтобы знать индексы
        foreach (MirFunction f in m.Functions)
        {
            vm.AddFunction(
                new VmFunction(
                    name: f.Name,
                    arity: f.ParamRegs.Count));
        }

        for (int i = 0; i < m.Functions.Count; i++)
        {
            LowerFunction(
                f: m.Functions[i],
                vf: vm.Functions[i],
                vm: vm);
        }

        return vm;
    }

    private static void LowerFunction(
        MirFunction f,
        VmFunction vf,
        VmModule vm)
    {
        // vreg -> local index
        var loc = new Dictionary<int, int>();

        int NextLocal() =>
            loc.Count;

        // Параметры → локалы (в порядке ParamRegs)
        foreach (VReg pr in f.ParamRegs)
        {
            if (!loc.ContainsKey(pr.Id))
            {
                loc[pr.Id] = NextLocal();
            }

            vf.ParamLocalIndices.Add(loc[pr.Id]);
        }

        List<Instr> code = vf.Code;
        var label = new Dictionary<MirBlock, int>();
        var brFixups = new List<(int pc, MirBlock target)>();
        var condFixups = new List<(int pcTrue, MirBlock t, int pcFalse, MirBlock fblk)>();

        // локальный loader
        void LdOp(
            MOperand op)
        {
            switch (op)
            {
                case Const c:
                    switch (c.Value)
                    {
                        case null:
                            code.Add(new Instr { Op = OpCode.LdcNull });

                            break;
                        case long n:
                            code.Add(new Instr { Op = OpCode.LdcI64, Imm = n });

                            break;
                        case bool b:
                            code.Add(
                                new Instr
                                {
                                    Op = OpCode.LdcBool, Imm = b
                                        ? 1
                                        : 0
                                });

                            break;
                        case char ch:
                            code.Add(new Instr { Op = OpCode.LdcChar, Imm = ch });

                            break;
                        case string s:
                            int id = vm.AddString(s);
                            code.Add(new Instr { Op = OpCode.LdcStr, Idx = id });

                            break;
                        default:
                            throw new NotSupportedException($"const {c.Value?.GetType().Name}");
                    }

                    break;

                case VReg v:
                    if (!loc.TryGetValue(
                            key: v.Id,
                            value: out int idx))
                    {
                        idx = NextLocal();
                        loc[v.Id] = idx;
                    }

                    code.Add(new Instr { Op = OpCode.LdLoc, A = idx });

                    break;

                default:
                    throw new NotSupportedException(
                        op.GetType()
                            .Name);
            }
        }

        void StDst(
            VReg v)
        {
            if (!loc.TryGetValue(
                    key: v.Id,
                    value: out int idx))
            {
                idx = NextLocal();
                loc[v.Id] = idx;
            }

            code.Add(new Instr { Op = OpCode.StLoc, A = idx });
        }

        // Пройти блоки в порядке объявления
        for (int bIdx = 0; bIdx < f.Blocks.Count; bIdx++)
        {
            MirBlock b = f.Blocks[bIdx];
            bool isLastBlock = bIdx == f.Blocks.Count - 1;
            label[b] = code.Count;

            foreach (MirInstr ins in b.Instructions)
            {
                switch (ins)
                {
                    case Move mv:
                        LdOp(mv.Src);
                        StDst(mv.Dst);

                        break;

                    case Bin bi:
                        LdOp(bi.L);
                        LdOp(bi.R);
                        code.Add(
                            new Instr
                            {
                                Op = bi.Op switch
                                {
                                    MBinOp.Add => OpCode.Add, MBinOp.Sub => OpCode.Sub,
                                    MBinOp.Mul => OpCode.Mul,
                                    MBinOp.Div => OpCode.Div, MBinOp.Mod => OpCode.Mod,
                                    MBinOp.Lt => OpCode.Lt, MBinOp.Le => OpCode.Le, MBinOp.Gt => OpCode.Gt,
                                    MBinOp.Ge => OpCode.Ge, MBinOp.Eq => OpCode.Eq, MBinOp.Ne => OpCode.Ne,
                                    _ => throw new NotSupportedException(bi.Op.ToString())
                                }
                            });

                        StDst(bi.Dst);

                        break;

                    case Un un:
                        LdOp(un.X);

                        switch (un.Op)
                        {
                            case MUnOp.Neg:
                                code.Add(new Instr { Op = OpCode.Neg });

                                break;
                            case MUnOp.Plus:
                                // no-op for unary plus
                                break;
                            case MUnOp.Not:
                                code.Add(new Instr { Op = OpCode.Not });

                                break;
                            default:
                                throw new NotSupportedException(un.Op.ToString());
                        }

                        StDst(un.Dst);

                        break;

                    case LoadIndex li:
                        LdOp(li.Arr);
                        LdOp(li.Index);
                        code.Add(new Instr { Op = OpCode.LdElem });
                        StDst(li.Dst);

                        break;

                    case StoreIndex si:
                        LdOp(si.Arr);
                        LdOp(si.Index);
                        LdOp(si.Value);
                        code.Add(new Instr { Op = OpCode.StElem });

                        break;

                    case Call cl:
                        {
                            // args
                            foreach (MOperand a in cl.Args)
                            {
                                LdOp(a);
                            }

                            if (vm.TryGetFunctionIndex(
                                    name: cl.Callee,
                                    idx: out int fidx))
                            {
                                code.Add(new Instr { Op = OpCode.CallUser, A = fidx, B = cl.Args.Count });
                            }
                            else
                            {
                                int sid = vm.AddString(cl.Callee);
                                code.Add(new Instr { Op = OpCode.CallBuiltin, A = sid, B = cl.Args.Count });
                            }

                            if (cl.Dst is null)
                            {
                                code.Add(new Instr { Op = OpCode.Pop });
                            }
                            else
                            {
                                StDst(cl.Dst);
                            }

                            break;
                        }

                    default:
                        throw new NotSupportedException(
                            ins.GetType()
                                .Name);
                }
            }

            // terminator (allow fallthrough if null; last block gets implicit ret null)
            if (b.Terminator is null)
            {
                if (isLastBlock)
                {
                    code.Add(new Instr { Op = OpCode.LdcNull });
                    code.Add(new Instr { Op = OpCode.Ret });
                }

                // otherwise: fall through to next block by linear order
            }
            else
            {
                switch (b.Terminator)
                {
                    case Ret r:
                        if (r.Value is null)
                        {
                            code.Add(new Instr { Op = OpCode.LdcNull });
                        }
                        else
                        {
                            LdOp(r.Value);
                        }

                        code.Add(new Instr { Op = OpCode.Ret });

                        break;

                    case Br br:
                        {
                            int at = code.Count;
                            code.Add(new Instr { Op = OpCode.Br, A = -1 });
                            brFixups.Add((at, br.Target));

                            break;
                        }

                    case BrCond bc:
                        {
                            LdOp(bc.Cond);
                            int atT = code.Count;
                            code.Add(new Instr { Op = OpCode.BrTrue, A = -1 });
                            int atF = code.Count;
                            code.Add(new Instr { Op = OpCode.Br, A = -1 });
                            condFixups.Add((atT, bc.IfTrue, atF, bc.IfFalse));

                            break;
                        }

                    default:
                        throw new NotSupportedException(
                            b.Terminator.GetType()
                                .Name);
                }
            }
        }

        // фиксапы переходов
        foreach ((int pc, MirBlock tgt) in brFixups)
        {
            vf.Code[pc] = vf.Code[pc] with { A = label[tgt] };
        }

        foreach ((int pcT, MirBlock t, int pcF, MirBlock fblk) in condFixups)
        {
            vf.Code[pcT] = vf.Code[pcT] with { A = label[t] };
            vf.Code[pcF] = vf.Code[pcF] with { A = label[fblk] };
        }

        vf.NLocals = loc.Count;
    }
}
