using Compiler.Frontend.Translation.MIR.Common;
using Compiler.Frontend.Translation.MIR.Instructions;
using Compiler.Frontend.Translation.MIR.Instructions.Abstractions;
using Compiler.Frontend.Translation.MIR.Operands;

namespace Compiler.Frontend.Translation.MIR;

/// Минимальная типизация для ускорения IL: i64-арифметика, булевы сравнения/логика, char-константы.
public sealed class MirTypeAnnotator
{
    public void Annotate(MirModule m)
    {
        foreach (var f in m.Functions) Annotate(f);
    }

    public void Annotate(MirFunction f)
    {
        // Параметры по умолчанию — Obj (можно расширить позже)
        foreach (var vr in f.ParamRegs) SetIfEmpty(f, vr, MirFunction.MType.Obj);

        bool changed;
        var guard = 0;
        do
        {
            changed = false;
            guard++;
            if (guard > 50) break; // safety

            foreach (var b in f.Blocks)
            foreach (var i in b.Instructions)
            {
                switch (i)
                {
                    case Move mv:
                        changed |= Merge(f, mv.Dst, TypeOfOperand(f, mv.Src));
                        break;

                    case Bin bi:
                        if (IsI64Arith(bi.Op))
                            changed |= Merge(f, bi.Dst, MirFunction.MType.I64);
                        else if (IsCmp(bi.Op))
                            changed |= Merge(f, bi.Dst, MirFunction.MType.Bool);
                        else
                            changed |= Merge(f, bi.Dst, MirFunction.MType.Obj);
                        break;

                    case Un un:
                        if (un.Op is MUnOp.Neg or MUnOp.Plus)
                            changed |= Merge(f, un.Dst, MirFunction.MType.I64);
                        else if (un.Op is MUnOp.Not)
                            changed |= Merge(f, un.Dst, MirFunction.MType.Bool);
                        else
                            changed |= Merge(f, un.Dst, MirFunction.MType.Obj);
                        break;

                    case LoadIndex li:
                        changed |= Merge(f, li.Dst, MirFunction.MType.Obj);
                        break;

                    case Call cl:
                        changed |= Merge(f, cl.Dst, GuessCallReturnType(cl.Callee));
                        break;
                }
            }
        } while (changed);
    }

    private static MirFunction.MType GuessCallReturnType(string callee) => callee switch
    {
        "len" => MirFunction.MType.I64,
        "ord" => MirFunction.MType.I64,
        "clock_ms" => MirFunction.MType.I64,
        "chr" => MirFunction.MType.Char,
        "print" => MirFunction.MType.Obj,
        "assert" => MirFunction.MType.Obj,
        _ => MirFunction.MType.Obj
    };
    private static bool IsCmp(MBinOp op) =>
        op is MBinOp.Lt or MBinOp.Le or MBinOp.Gt or MBinOp.Ge or MBinOp.Eq or MBinOp.Ne;

    private static bool IsI64Arith(MBinOp op) =>
        op is MBinOp.Add or MBinOp.Sub or MBinOp.Mul or MBinOp.Div or MBinOp.Mod;

    private static bool Merge(MirFunction f, VReg? dst, MirFunction.MType t)
    {
        if (dst is null) return false;
        if (!f.Types.TryGetValue(dst.Id, out var cur))
        {
            f.Types[dst.Id] = t;
            return true;
        }
        if (cur == t) return false;

        // Упрощённое расширение: любой конфликт → Obj
        var widened = cur == MirFunction.MType.Obj || t == MirFunction.MType.Obj ? MirFunction.MType.Obj : cur == t ? cur : MirFunction.MType.Obj;
        if (widened != cur)
        {
            f.Types[dst.Id] = widened;
            return true;
        }
        return false;
    }

    private static void SetIfEmpty(MirFunction f, VReg? v, MirFunction.MType t)
    {
        f.Types.TryAdd(v.Id, t);
    }

    private static MirFunction.MType TypeOfOperand(MirFunction f, MOperand? op) => op switch
    {
        VReg v => f.Types.GetValueOrDefault(v.Id, MirFunction.MType.Obj),
        Const c => c.Value switch
        {
            long => MirFunction.MType.I64, bool => MirFunction.MType.Bool, char => MirFunction.MType.Char, _ => MirFunction.MType.Obj
        },
        _ => MirFunction.MType.Obj
    };
}
