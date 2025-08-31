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
    public void Run(
        MirModule m)
    {
        foreach (MirFunction f in m.Functions)
        {
            SimplifyFunction(f);
        }
    }

    public void SimplifyFunction(
        MirFunction f)
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
                            MOperand src = Resolve(
                                env: env,
                                op: mv.Src);

                            // kill old mapping for dst (it will be overwritten now)
                            env[mv.Dst.Id] = src;

                            // Skip no-op move: v <- v
                            if (src is VReg v && v.Id == mv.Dst.Id)
                            {
                                break;
                            }

                            newIns.Add(
                                new Move(
                                    Dst: mv.Dst,
                                    Src: src));

                            break;
                        }

                    case Bin bi:
                        {
                            MOperand l = Resolve(
                                env: env,
                                op: bi.L);

                            MOperand r = Resolve(
                                env: env,
                                op: bi.R);

                            if (l is Const lc && r is Const rc && TryEvalBin(
                                    op: bi.Op,
                                    l: lc.Value,
                                    r: rc.Value,
                                    res: out object? res))
                            {
                                var c = new Const(res);
                                env[bi.Dst.Id] = c;
                                newIns.Add(
                                    new Move(
                                        Dst: bi.Dst,
                                        Src: c));
                            }
                            else
                            {
                                // use propagated operands
                                newIns.Add(
                                    new Bin(
                                        Dst: bi.Dst,
                                        Op: bi.Op,
                                        L: l,
                                        R: r));

                                // dst gets a fresh unknown value, forget previous binding
                                env.Remove(bi.Dst.Id);
                            }

                            break;
                        }

                    case Un un:
                        {
                            MOperand x = Resolve(
                                env: env,
                                op: un.X);

                            if (x is Const xc && TryEvalUn(
                                    op: un.Op,
                                    x: xc.Value,
                                    res: out object? res))
                            {
                                var c = new Const(res);
                                env[un.Dst.Id] = c;
                                newIns.Add(
                                    new Move(
                                        Dst: un.Dst,
                                        Src: c));
                            }
                            else
                            {
                                newIns.Add(
                                    new Un(
                                        Dst: un.Dst,
                                        Op: un.Op,
                                        X: x));

                                env.Remove(un.Dst.Id);
                            }

                            break;
                        }

                    case LoadIndex li:
                        {
                            MOperand arr = Resolve(
                                env: env,
                                op: li.Arr);

                            MOperand idx = Resolve(
                                env: env,
                                op: li.Index);

                            newIns.Add(
                                new LoadIndex(
                                    Dst: li.Dst,
                                    Arr: arr,
                                    Index: idx));

                            env.Remove(li.Dst.Id);

                            break;
                        }

                    case StoreIndex si:
                        {
                            MOperand arr = Resolve(
                                env: env,
                                op: si.Arr);

                            MOperand idx = Resolve(
                                env: env,
                                op: si.Index);

                            MOperand val = Resolve(
                                env: env,
                                op: si.Value);

                            newIns.Add(
                                new StoreIndex(
                                    Arr: arr,
                                    Index: idx,
                                    Value: val));

                            break;
                        }

                    case Call cl:
                        {
                            var args = new List<MOperand>(cl.Args.Count);

                            foreach (MOperand a in cl.Args)
                            {
                                args.Add(
                                    Resolve(
                                        env: env,
                                        op: a));
                            }

                            newIns.Add(
                                new Call(
                                    Dst: cl.Dst,
                                    Callee: cl.Callee,
                                    Args: args));

                            if (cl.Dst is not null)
                            {
                                env.Remove(cl.Dst.Id);
                            }

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
                MOperand cond = ResolveFromVRegOnly(
                    op: bc.Cond,
                    env: env);

                if (cond is Const c && c.Value is bool bb)
                {
                    b.Terminator = new Br(
                        bb
                            ? bc.IfTrue
                            : bc.IfFalse);
                }
            }
        }
    }

    private static bool IsConstCmpSupported(
        object? l,
        object? r)
    {
        if (l is null || r is null)
        {
            return true;
        }

        Type tl = l.GetType();
        Type tr = r.GetType();

        if (tl != tr)
        {
            return false;
        }

        return tl == typeof(long) || tl == typeof(bool) || tl == typeof(char) || tl == typeof(string);
    }

    private static MOperand Resolve(
        Dictionary<int, MOperand> env,
        MOperand op)
    {
        if (op is VReg v && env.TryGetValue(
                key: v.Id,
                value: out MOperand? mapped))
        {
            return mapped;
        }

        return op;
    }

    // For terminators we only consider direct VReg -> Const mapping (no deep chains),
    // because side effects across blocks are not tracked here.
    private static MOperand ResolveFromVRegOnly(
        MOperand op,
        Dictionary<int, MOperand> env)
    {
        return op is VReg v && env.TryGetValue(
            key: v.Id,
            value: out MOperand? mapped)
            ? mapped
            : op;
    }

    private static bool TryEvalBin(
        MBinOp op,
        object? l,
        object? r,
        out object? res)
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
                if (IsConstCmpSupported(
                        l: l,
                        r: r))
                {
                    res = Equals(
                        objA: l,
                        objB: r);

                    return true;
                }

                return false;
            case MBinOp.Ne:
                if (IsConstCmpSupported(
                        l: l,
                        r: r))
                {
                    res = !Equals(
                        objA: l,
                        objB: r);

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

    private static bool TryEvalUn(
        MUnOp op,
        object? x,
        out object? res)
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
