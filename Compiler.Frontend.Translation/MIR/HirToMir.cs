using Compiler.Frontend.Translation.HIR.Common;
using Compiler.Frontend.Translation.HIR.Expressions;
using Compiler.Frontend.Translation.HIR.Expressions.Abstractions;
using Compiler.Frontend.Translation.HIR.Statements;
using Compiler.Frontend.Translation.HIR.Statements.Abstractions;
using Compiler.Frontend.Translation.MIR.Common;
using Compiler.Frontend.Translation.MIR.Instructions;
using Compiler.Frontend.Translation.MIR.Instructions.Abstractions;
using Compiler.Frontend.Translation.MIR.Operands;

namespace Compiler.Frontend.Translation.MIR;

public sealed class HirToMir
{
    public MirModule Lower(
        ProgramHir prog)
    {
        var ctx = new Ctx();

        foreach (FuncHir f in prog.Functions)
        {
            ctx.Mod.Functions.Add(
                LowerFunc(
                    ctx: ctx,
                    f: f));
        }

        return ctx.Mod;
    }

    private MOperand? LowerExpr(
        Ctx ctx,
        ExprHir e)
    {
        switch (e)
        {
            case IntHir i:
                return new Const(i.Value);
            case BoolHir b:
                return new Const(b.Value);
            case CharHir c:
                return new Const(c.Value);
            case StringHir s:
                return new Const(s.Value);

            case VarHir v:
                if (!ctx.TryGet(
                        name: v.Name,
                        vr: out VReg? reg))
                {
                    throw new InvalidOperationException($"variable '{v.Name}' not in scope during MIR lowering");
                }

                return reg;

            case UnHir u:
                {
                    MOperand? x = LowerExpr(
                        ctx: ctx,
                        e: u.Operand);

                    VReg? dst = ctx.F.NewTemp();
                    ctx.Cur.Instructions.Add(
                        new Un(
                            Dst: dst,
                            Op: MapUn(u.Op),
                            X: x));

                    return dst;
                }

            case BinHir { Op: BinOp.Assign } b:
                {
                    MOperand? rhs = LowerExpr(
                        ctx: ctx,
                        e: b.Right);

                    switch (b.Left)
                    {
                        case VarHir lv when ctx.TryGet(
                            name: lv.Name,
                            vr: out VReg? lreg):
                            ctx.Cur.Instructions.Add(
                                new Move(
                                    Dst: lreg,
                                    Src: rhs));

                            return lreg;
                        case IndexHir ix:
                            {
                                MOperand? arr = LowerExpr(
                                    ctx: ctx,
                                    e: ix.Target);

                                MOperand? idx = LowerExpr(
                                    ctx: ctx,
                                    e: ix.Index);

                                ctx.Cur.Instructions.Add(
                                    new StoreIndex(
                                        Arr: arr,
                                        Index: idx,
                                        Value: rhs));

                                return rhs;
                            }
                        default:
                            throw new NotSupportedException("assignment target not supported");
                    }
                }

            case BinHir { Op: BinOp.And or BinOp.Or } b:
                return LowerShortCircuit(
                    ctx: ctx,
                    b: b);

            case BinHir b:
                {
                    MOperand? l = LowerExpr(
                        ctx: ctx,
                        e: b.Left);

                    MOperand? r = LowerExpr(
                        ctx: ctx,
                        e: b.Right);

                    VReg? dst = ctx.F.NewTemp();
                    ctx.Cur.Instructions.Add(
                        new Bin(
                            Dst: dst,
                            Op: MapBin(b.Op),
                            L: l,
                            R: r));

                    return dst;
                }

            case CallHir c:
                {
                    string name = (c.Callee as VarHir)?.Name
                        ?? throw new NotSupportedException("only simple function names are callable");

                    List<MOperand> args = c
                        .Args
                        .Select(a => LowerExpr(
                            ctx: ctx,
                            e: a))
                        .ToList();

                    VReg? dst = ctx.F.NewTemp();
                    ctx.Cur.Instructions.Add(
                        new Call(
                            Dst: dst,
                            Callee: name,
                            Args: args));

                    return dst;
                }

            case IndexHir ix:
                {
                    MOperand? arr = LowerExpr(
                        ctx: ctx,
                        e: ix.Target);

                    MOperand? idx = LowerExpr(
                        ctx: ctx,
                        e: ix.Index);

                    VReg? dst = ctx.F.NewTemp();
                    ctx.Cur.Instructions.Add(
                        new LoadIndex(
                            Dst: dst,
                            Arr: arr,
                            Index: idx));

                    return dst;
                }

            default:
                throw new NotSupportedException($"Expr {e.GetType().Name} not supported in MIR lowering");
        }
    }

    private MirFunction LowerFunc(
        Ctx ctx,
        FuncHir f)
    {
        ctx.F = new MirFunction(f.Name);
        ctx.Cur = ctx.F.NewBlock("entry");

        ctx.PushScope();

        foreach (string p in f.Parameters)
        {
            VReg? vr = ctx.Def(p);
            ctx.F.ParamNames.Add(p);
            ctx.F.ParamRegs.Add(vr);
        }

        LowerStmt(
            ctx: ctx,
            s: f.Body);

        // гарантируем терминатор
        if (ctx.Cur.Terminator is null)
        {
            ctx.Cur.Terminator = new Ret(null);
        }

        ctx.PopScope();

        return ctx.F;
    }

    private MOperand? LowerShortCircuit(
        Ctx ctx,
        BinHir b)
    {
        VReg? result = ctx.F.NewTemp();

        MOperand? evalL = LowerExpr(
            ctx: ctx,
            e: b.Left);

        MirBlock rhsB = ctx.F.NewBlock($"sc_rhs_{ctx.F.Blocks.Count}");
        MirBlock joinB = ctx.F.NewBlock($"sc_join_{ctx.F.Blocks.Count}");
        MirBlock shortB = ctx.F.NewBlock($"sc_short_{ctx.F.Blocks.Count}");

        if (b.Op == BinOp.And)
        {
            // if (!L) goto short; else goto rhs
            ctx.Cur.Terminator = new BrCond(
                Cond: evalL,
                IfTrue: rhsB,
                IfFalse: shortB);

            // short: result = false
            ctx.Cur = shortB;
            ctx.Cur.Instructions.Add(
                new Move(
                    Dst: result,
                    Src: new Const(false)));

            ctx.Cur.Terminator = new Br(joinB);

            // rhs: result = R
            ctx.Cur = rhsB;
            MOperand? r = LowerExpr(
                ctx: ctx,
                e: b.Right);

            ctx.Cur.Instructions.Add(
                new Move(
                    Dst: result,
                    Src: r));

            ctx.Cur.Terminator = new Br(joinB);
        }
        else // OR
        {
            // if (L) goto short; else goto rhs
            ctx.Cur.Terminator = new BrCond(
                Cond: evalL,
                IfTrue: shortB,
                IfFalse: rhsB);

            // short: result = true
            ctx.Cur = shortB;
            ctx.Cur.Instructions.Add(
                new Move(
                    Dst: result,
                    Src: new Const(true)));

            ctx.Cur.Terminator = new Br(joinB);

            // rhs: result = R
            ctx.Cur = rhsB;
            MOperand? r = LowerExpr(
                ctx: ctx,
                e: b.Right);

            ctx.Cur.Instructions.Add(
                new Move(
                    Dst: result,
                    Src: r));

            ctx.Cur.Terminator = new Br(joinB);
        }

        ctx.Cur = joinB;

        return result;
    }

    private void LowerStmt(
        Ctx ctx,
        StmtHir s)
    {
        switch (s)
        {
            case BlockHir b:
                ctx.PushScope();

                foreach (StmtHir st in b.Statements)
                {
                    LowerStmt(
                        ctx: ctx,
                        s: st);
                }

                ctx.PopScope();

                break;

            case LetHir v:
                {
                    VReg? dest = ctx.Def(v.Name);

                    if (v.Init is not null)
                    {
                        MOperand? src = LowerExpr(
                            ctx: ctx,
                            e: v.Init);

                        ctx.Cur.Instructions.Add(
                            new Move(
                                Dst: dest,
                                Src: src));
                    }

                    break;
                }

            case ExprStmtHir es:
                if (es.Expr is not null)
                {
                    _ = LowerExpr(
                        ctx: ctx,
                        e: es.Expr);
                }

                break;

            case ReturnHir r:
                {
                    MOperand? rv = r.Expr is null
                        ? null
                        : LowerExpr(
                            ctx: ctx,
                            e: r.Expr);

                    ctx.Cur.Terminator = new Ret(rv);
                    ctx.Cur = ctx.F.NewBlock($"dead_{ctx.F.Blocks.Count}");

                    break;
                }

            case IfHir iff:
                {
                    MirBlock thenB = ctx.F.NewBlock($"then_{ctx.F.Blocks.Count}");
                    MirBlock elseB = ctx.F.NewBlock($"else_{ctx.F.Blocks.Count}");
                    MirBlock joinB = ctx.F.NewBlock($"join_{ctx.F.Blocks.Count}");

                    MOperand? c = LowerExpr(
                        ctx: ctx,
                        e: iff.Cond);

                    ctx.Cur.Terminator = new BrCond(
                        Cond: c,
                        IfTrue: thenB,
                        IfFalse: iff.Else is null
                            ? joinB
                            : elseB);

                    // then
                    ctx.Cur = thenB;
                    LowerStmt(
                        ctx: ctx,
                        s: iff.Then);

                    if (ctx.Cur.Terminator is null)
                    {
                        ctx.Cur.Terminator = new Br(joinB);
                    }

                    // else
                    if (iff.Else is not null)
                    {
                        ctx.Cur = elseB;
                        LowerStmt(
                            ctx: ctx,
                            s: iff.Else);

                        if (ctx.Cur.Terminator is null)
                        {
                            ctx.Cur.Terminator = new Br(joinB);
                        }
                    }

                    // continue at join
                    ctx.Cur = joinB;

                    break;
                }

            case WhileHir w:
                {
                    MirBlock head = ctx.F.NewBlock($"head_{ctx.F.Blocks.Count}");
                    MirBlock body = ctx.F.NewBlock($"body_{ctx.F.Blocks.Count}");
                    MirBlock exit = ctx.F.NewBlock($"exit_{ctx.F.Blocks.Count}");

                    ctx.Cur.Terminator = new Br(head);

                    ctx.Cur = head;
                    MOperand? c = LowerExpr(
                        ctx: ctx,
                        e: w.Cond);

                    ctx.Cur.Terminator = new BrCond(
                        Cond: c,
                        IfTrue: body,
                        IfFalse: exit);

                    ctx.Loops.Push((exit, head));
                    ctx.Cur = body;
                    LowerStmt(
                        ctx: ctx,
                        s: w.Body);

                    if (ctx.Cur.Terminator is null)
                    {
                        ctx.Cur.Terminator = new Br(head);
                    }

                    ctx.Loops.Pop();

                    ctx.Cur = exit;

                    break;
                }

            case BreakHir:
                {
                    (MirBlock brk, _) = ctx.Loops.Peek();
                    ctx.Cur.Terminator = new Br(brk);
                    ctx.Cur = ctx.F.NewBlock($"dead_{ctx.F.Blocks.Count}");

                    break;
                }

            case ContinueHir:
                {
                    (_, MirBlock cont) = ctx.Loops.Peek();
                    ctx.Cur.Terminator = new Br(cont);
                    ctx.Cur = ctx.F.NewBlock($"dead_{ctx.F.Blocks.Count}");

                    break;
                }

            default:
                throw new NotSupportedException($"Stmt {s.GetType().Name} not supported in MIR lowering");
        }
    }

    private static MBinOp MapBin(
        BinOp op)
    {
        return op switch
        {
            BinOp.Add => MBinOp.Add, BinOp.Sub => MBinOp.Sub, BinOp.Mul => MBinOp.Mul,
            BinOp.Div => MBinOp.Div, BinOp.Mod => MBinOp.Mod,
            BinOp.Lt => MBinOp.Lt, BinOp.Le => MBinOp.Le, BinOp.Gt => MBinOp.Gt, BinOp.Ge => MBinOp.Ge,
            BinOp.Eq => MBinOp.Eq, BinOp.Ne => MBinOp.Ne,
            _ => throw new NotSupportedException($"bin op {op} not supported in MIR")
        };
    }

    private static MUnOp MapUn(
        UnOp op)
    {
        return op switch
        {
            UnOp.Neg => MUnOp.Neg, UnOp.Not => MUnOp.Not, UnOp.Plus => MUnOp.Plus,
            _ => throw new NotSupportedException($"un op {op} not supported in MIR")
        };
    }

    private sealed class Ctx
    {
        public MirBlock Cur = null!;
        public MirFunction F = null!;
        public readonly Stack<(MirBlock brk, MirBlock cont)> Loops = new Stack<(MirBlock brk, MirBlock cont)>();
        public readonly MirModule Mod = new MirModule();
        public readonly Stack<Dictionary<string, VReg?>> Scopes = new Stack<Dictionary<string, VReg?>>();
        public VReg? Def(
            string name)
        {
            VReg? vr = F.NewTemp();
            Scopes
                .Peek()[name] = vr;

            return vr;
        }
        public void PopScope()
        {
            Scopes.Pop();
        }

        public void PushScope()
        {
            Scopes.Push(new Dictionary<string, VReg?>());
        }

        public bool TryGet(
            string name,
            out VReg? vr)
        {
            foreach (Dictionary<string, VReg?> s in Scopes)
            {
                if (s.TryGetValue(
                        key: name,
                        value: out vr))
                {
                    return true;
                }
            }

            vr = null!;

            return false;
        }
    }
}
