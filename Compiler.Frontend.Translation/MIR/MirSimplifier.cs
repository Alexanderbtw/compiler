using Compiler.Frontend.Translation.MIR.Common;
using Compiler.Frontend.Translation.MIR.Instructions;
using Compiler.Frontend.Translation.MIR.Instructions.Abstractions;
using Compiler.Frontend.Translation.MIR.Operands;

namespace Compiler.Frontend.Translation.MIR;

/// <summary>
///     MIR simplification: constant folding, copy-propagation inside basic blocks,
///     and simple branch folding (BrCond with const bool).
///     Conservative by design (no reordering, no DCE beyond removing no-op moves).
/// </summary>
public sealed class MirSimplifier
{
    public void Run(MirModule m)
    {
        foreach (MirFunction f in m.Functions)
            SimplifyFunction(f);
    }

    public void SimplifyFunction(MirFunction f)
    {
        foreach (MirBlock b in f.Blocks)
        {
            // env maps vreg.Id -> MOperand (Const or VReg)
            var env = new Dictionary<int, MOperand>();
            var newIns = new List<MirInstr>(b.Instructions.Count);

            foreach (MirInstr ins in b.Instructions)
            {
                switch (ins)
                {
                    case Move mv:
                    {
                        MOperand src = Resolve(env, mv.Src);

                        // kill old mapping for dst (it will be overwritten now)
                        env[mv.Dst.Id] = src;

                        // Skip no-op move: v <- v
                        if (src is VReg v && v.Id == mv.Dst.Id)
                            break;
                        newIns.Add(new Move(mv.Dst, src));
                        break;
                    }

                    case Bin bi:
                    {
                        MOperand l = Resolve(env, bi.L);
                        MOperand r = Resolve(env, bi.R);
                        if (l is Const lc && r is Const rc && TryEvalBin(
                                bi.Op,
                                lc.Value,
                                rc.Value,
                                out object? res))
                        {
                            var c = new Const(res);
                            env[bi.Dst.Id] = c;
                            newIns.Add(new Move(bi.Dst, c));
                        }
                        else
                        {
                            // use propagated operands
                            newIns.Add(new Bin(bi.Dst, bi.Op, l, r));

                            // dst gets a fresh unknown value, forget previous binding
                            env.Remove(bi.Dst.Id);
                        }
                        break;
                    }

                    case Un un:
                    {
                        MOperand x = Resolve(env, un.X);
                        if (x is Const xc && TryEvalUn(un.Op, xc.Value, out object? res))
                        {
                            var c = new Const(res);
                            env[un.Dst.Id] = c;
                            newIns.Add(new Move(un.Dst, c));
                        }
                        else
                        {
                            newIns.Add(new Un(un.Dst, un.Op, x));
                            env.Remove(un.Dst.Id);
                        }
                        break;
                    }

                    case LoadIndex li:
                    {
                        MOperand arr = Resolve(env, li.Arr);
                        MOperand idx = Resolve(env, li.Index);
                        newIns.Add(new LoadIndex(li.Dst, arr, idx));
                        env.Remove(li.Dst.Id);
                        break;
                    }

                    case StoreIndex si:
                    {
                        MOperand arr = Resolve(env, si.Arr);
                        MOperand idx = Resolve(env, si.Index);
                        MOperand val = Resolve(env, si.Value);
                        newIns.Add(new StoreIndex(arr, idx, val));
                        break;
                    }

                    case Call cl:
                    {
                        var args = new List<MOperand>(cl.Args.Count);
                        foreach (MOperand a in cl.Args) args.Add(Resolve(env, a));
                        newIns.Add(new Call(cl.Dst, cl.Callee, args));
                        if (cl.Dst is not null) env.Remove(cl.Dst.Id);
                        break;
                    }

                    default:
                        newIns.Add(ins);
                        break;
                }
            }

            b.Instructions.Clear();
            b.Instructions.AddRange(newIns);

            // Terminator simplification: BrCond with const bool
            if (b.Terminator is BrCond bc)
            {
                MOperand cond = ResolveFromVRegOnly(bc.Cond, env);
                if (cond is Const c && c.Value is bool bb)
                {
                    b.Terminator = new Br(bb ? bc.IfTrue : bc.IfFalse);
                }
            }
        }
    }

    private static bool IsConstCmpSupported(object? l, object? r)
    {
        if (l is null || r is null) return true;
        Type tl = l.GetType();
        Type tr = r.GetType();
        if (tl != tr) return false;
        return tl == typeof(long) || tl == typeof(bool) || tl == typeof(char) || tl == typeof(string);
    }

    private static MOperand Resolve(Dictionary<int, MOperand> env, MOperand op)
    {
        if (op is VReg v && env.TryGetValue(v.Id, out MOperand? mapped))
            return mapped;
        return op;
    }

    // For terminators we only consider direct VReg -> Const mapping (no deep chains),
    // because side effects across blocks are not tracked here.
    private static MOperand ResolveFromVRegOnly(MOperand op, Dictionary<int, MOperand> env) =>
        op is VReg v && env.TryGetValue(v.Id, out MOperand? mapped) ? mapped : op;

    private static bool TryEvalBin(MBinOp op, object? l, object? r, out object? res)
    {
        res = null;
        switch (op)
        {
            // integer arithmetic
            case MBinOp.Add:
                if (l is long la && r is long ra)
                {
                    res = la + ra;
                    return true;
                }
                return false;
            case MBinOp.Sub:
                if (l is long ls && r is long rs)
                {
                    res = ls - rs;
                    return true;
                }
                return false;
            case MBinOp.Mul:
                if (l is long lm && r is long rm)
                {
                    res = lm * rm;
                    return true;
                }
                return false;
            case MBinOp.Div:
                if (l is long ld && r is long rd && rd != 0)
                {
                    res = ld / rd;
                    return true;
                }
                return false;
            case MBinOp.Mod:
                if (l is long lq && r is long rq && rq != 0)
                {
                    res = lq % rq;
                    return true;
                }
                return false;

            // comparisons
            case MBinOp.Eq:
                if (IsConstCmpSupported(l, r))
                {
                    res = Equals(l, r);
                    return true;
                }
                return false;
            case MBinOp.Ne:
                if (IsConstCmpSupported(l, r))
                {
                    res = !Equals(l, r);
                    return true;
                }
                return false;
            case MBinOp.Lt:
                if (l is long l1 && r is long r1)
                {
                    res = l1 < r1;
                    return true;
                }
                return false;
            case MBinOp.Le:
                if (l is long l2 && r is long r2)
                {
                    res = l2 <= r2;
                    return true;
                }
                return false;
            case MBinOp.Gt:
                if (l is long l3 && r is long r3)
                {
                    res = l3 > r3;
                    return true;
                }
                return false;
            case MBinOp.Ge:
                if (l is long l4 && r is long r4)
                {
                    res = l4 >= r4;
                    return true;
                }
                return false;

            default:
                return false;
        }
    }

    private static bool TryEvalUn(MUnOp op, object? x, out object? res)
    {
        res = null;
        switch (op)
        {
            case MUnOp.Plus:
                if (x is long lx)
                {
                    res = +lx;
                    return true;
                }
                return false;
            case MUnOp.Neg:
                if (x is long nx)
                {
                    res = -nx;
                    return true;
                }
                return false;
            case MUnOp.Not:
                if (x is bool bx)
                {
                    res = !bx;
                    return true;
                }
                return false;
            default:
                return false;
        }
    }
}
