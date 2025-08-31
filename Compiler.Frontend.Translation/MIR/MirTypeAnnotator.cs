using Compiler.Frontend.Translation.MIR.Common;
using Compiler.Frontend.Translation.MIR.Instructions;
using Compiler.Frontend.Translation.MIR.Instructions.Abstractions;
using Compiler.Frontend.Translation.MIR.Operands;

namespace Compiler.Frontend.Translation.MIR;

/// Минимальная типизация для ускорения IL: i64-арифметика, булевы сравнения/логика, char-константы.
public sealed class MirTypeAnnotator
{
    public void Annotate(
        MirModule m)
    {
        foreach (MirFunction f in m.Functions)
        {
            Annotate(f);
        }
    }

    public void Annotate(
        MirFunction f)
    {
        // Параметры по умолчанию — Obj (можно расширить позже)
        foreach (VReg vr in f.ParamRegs)
        {
            SetIfEmpty(
                f: f,
                v: vr,
                t: MirFunction.MType.Obj);
        }

        bool changed;
        int guard = 0;

        do
        {
            changed = false;
            guard++;

            if (guard > 50)
            {
                break; // safety
            }

            foreach (MirBlock b in f.Blocks)
            foreach (MirInstr i in b.Instructions)
            {
                switch (i)
                {
                    case Move mv:
                        changed |= Merge(
                            f: f,
                            dst: mv.Dst,
                            t: TypeOfOperand(
                                f: f,
                                op: mv.Src));

                        break;

                    case Bin bi:
                        if (IsI64Arith(bi.Op))
                        {
                            changed |= Merge(
                                f: f,
                                dst: bi.Dst,
                                t: MirFunction.MType.I64);
                        }
                        else if (IsCmp(bi.Op))
                        {
                            changed |= Merge(
                                f: f,
                                dst: bi.Dst,
                                t: MirFunction.MType.Bool);
                        }
                        else
                        {
                            changed |= Merge(
                                f: f,
                                dst: bi.Dst,
                                t: MirFunction.MType.Obj);
                        }

                        break;

                    case Un un:
                        if (un.Op is MUnOp.Neg or MUnOp.Plus)
                        {
                            changed |= Merge(
                                f: f,
                                dst: un.Dst,
                                t: MirFunction.MType.I64);
                        }
                        else if (un.Op is MUnOp.Not)
                        {
                            changed |= Merge(
                                f: f,
                                dst: un.Dst,
                                t: MirFunction.MType.Bool);
                        }
                        else
                        {
                            changed |= Merge(
                                f: f,
                                dst: un.Dst,
                                t: MirFunction.MType.Obj);
                        }

                        break;

                    case LoadIndex li:
                        changed |= Merge(
                            f: f,
                            dst: li.Dst,
                            t: MirFunction.MType.Obj);

                        break;

                    case Call cl:
                        changed |= Merge(
                            f: f,
                            dst: cl.Dst,
                            t: GuessCallReturnType(cl.Callee));

                        break;
                }
            }
        }
        while (changed);
    }

    private static MirFunction.MType GuessCallReturnType(
        string callee)
    {
        return callee switch
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
    private static bool IsCmp(
        MBinOp op)
    {
        return op is MBinOp.Lt or MBinOp.Le or MBinOp.Gt or MBinOp.Ge or MBinOp.Eq or MBinOp.Ne;
    }

    private static bool IsI64Arith(
        MBinOp op)
    {
        return op is MBinOp.Add or MBinOp.Sub or MBinOp.Mul or MBinOp.Div or MBinOp.Mod;
    }

    private static bool Merge(
        MirFunction f,
        VReg? dst,
        MirFunction.MType t)
    {
        if (dst is null)
        {
            return false;
        }

        if (!f.Types.TryGetValue(
                key: dst.Id,
                value: out MirFunction.MType cur))
        {
            f.Types[dst.Id] = t;

            return true;
        }

        if (cur == t)
        {
            return false;
        }

        // Упрощённое расширение: любой конфликт → Obj
        MirFunction.MType widened = cur == MirFunction.MType.Obj || t == MirFunction.MType.Obj ? MirFunction.MType.Obj : cur == t ? cur : MirFunction.MType.Obj;

        if (widened != cur)
        {
            f.Types[dst.Id] = widened;

            return true;
        }

        return false;
    }

    private static void SetIfEmpty(
        MirFunction f,
        VReg? v,
        MirFunction.MType t)
    {
        f.Types.TryAdd(
            key: v.Id,
            value: t);
    }

    private static MirFunction.MType TypeOfOperand(
        MirFunction f,
        MOperand? op)
    {
        return op switch
        {
            VReg v => f.Types.GetValueOrDefault(
                key: v.Id,
                defaultValue: MirFunction.MType.Obj),
            Const c => c.Value switch
            {
                long => MirFunction.MType.I64, bool => MirFunction.MType.Bool, char => MirFunction.MType.Char, _ => MirFunction.MType.Obj
            },
            _ => MirFunction.MType.Obj
        };
    }
}
