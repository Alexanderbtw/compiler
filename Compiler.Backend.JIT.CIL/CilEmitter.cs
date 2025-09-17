using System.Reflection;
using System.Reflection.Emit;

using Compiler.Backend.VM.Execution;
using Compiler.Backend.VM.Values;
using Compiler.Frontend.Translation.MIR.Common;
using Compiler.Frontend.Translation.HIR.Metadata;
using Compiler.Frontend.Translation.MIR.Instructions;
using Compiler.Frontend.Translation.MIR.Instructions.Abstractions;
using Compiler.Frontend.Translation.MIR.Operands;
using Compiler.Frontend.Translation.MIR.Operands.Abstractions;

namespace Compiler.Backend.JIT.CIL;

/// <summary>
///     Emits CLR DynamicMethod IL for MIR functions and returns VM-callable delegates
/// </summary>
internal sealed class CilEmitter
{
    private static readonly FieldInfo FiNull = typeof(Value).GetField(nameof(Value.Null))!;
    private static readonly MethodInfo MiArr = typeof(ValueOps).GetMethod(nameof(ValueOps.Arr))!;

    private static readonly MethodInfo MiBuiltins = typeof(BuiltinsVm)
        .GetMethod(
            name: nameof(BuiltinsVm.Invoke),
            types: [typeof(string), typeof(VmJitContext), typeof(Value[])])!;

    private static readonly MethodInfo MiEnter = typeof(VmJitContext).GetMethod(nameof(VmJitContext.EnterFrame))!;
    private static readonly MethodInfo MiEq = typeof(ValueOps).GetMethod(nameof(ValueOps.AreValuesEqual))!;
    private static readonly MethodInfo MiExit = typeof(VmJitContext).GetMethod(nameof(VmJitContext.ExitFrame))!;
    private static readonly MethodInfo MiFromBool = typeof(Value).GetMethod(nameof(Value.FromBool))!;
    private static readonly MethodInfo MiFromChar = typeof(Value).GetMethod(nameof(Value.FromChar))!;
    private static readonly MethodInfo MiFromLong = typeof(Value).GetMethod(nameof(Value.FromLong))!;
    private static readonly MethodInfo MiFromString = typeof(Value).GetMethod(nameof(Value.FromString))!;
    private static readonly MethodInfo MiGet = typeof(ValueOps).GetMethod(nameof(ValueOps.Get))!;
    private static readonly MethodInfo MiI64 = typeof(ValueOps).GetMethod(nameof(ValueOps.I64))!;
    private static readonly MethodInfo MiInvokeFn = typeof(VmJitContext).GetMethod(nameof(VmJitContext.InvokeFunction))!;
    private static readonly MethodInfo MiSet = typeof(ValueOps).GetMethod(nameof(ValueOps.Set))!;
    private static readonly MethodInfo MiToBool = typeof(ValueOps).GetMethod(nameof(ValueOps.ToBool))!;

    public static CilModule EmitModule(
        MirModule mir)
    {
        var map = new Dictionary<string, VmJitFunc>(
            capacity: mir.Functions.Count,
            comparer: StringComparer.Ordinal);

        foreach (MirFunction f in mir.Functions)
        {
            map[f.Name] = CompileFunction(f);
        }

        return new CilModule(map);
    }

    private static VmJitFunc CompileFunction(
        MirFunction func)
    {
        // Compute max vreg id for locals array sizing
        int maxId = -1;

        if (func.ParamRegs.Count > 0)
        {
            maxId = Math.Max(
                val1: maxId,
                val2: func.ParamRegs.Max(v => v.Id));
        }

        foreach (MirBlock b in func.Blocks)
        foreach (MirInstr ins in b.Instructions)
        {
            switch (ins)
            {
                case Move mv:
                    maxId = Math.Max(
                        val1: maxId,
                        val2: mv.Dst.Id);

                    if (mv.Src is VReg sv)
                    {
                        maxId = Math.Max(
                            val1: maxId,
                            val2: sv.Id);
                    }

                    break;
                case Bin bi:
                    maxId = Math.Max(
                        val1: maxId,
                        val2: bi.Dst.Id);

                    if (bi.L is VReg lv)
                    {
                        maxId = Math.Max(
                            val1: maxId,
                            val2: lv.Id);
                    }

                    if (bi.R is VReg rv)
                    {
                        maxId = Math.Max(
                            val1: maxId,
                            val2: rv.Id);
                    }

                    break;
                case Un un:
                    maxId = Math.Max(
                        val1: maxId,
                        val2: un.Dst.Id);

                    if (un.X is VReg xv)
                    {
                        maxId = Math.Max(
                            val1: maxId,
                            val2: xv.Id);
                    }

                    break;
                case LoadIndex li:
                    maxId = Math.Max(
                        val1: maxId,
                        val2: li.Dst.Id);

                    if (li.Arr is VReg av)
                    {
                        maxId = Math.Max(
                            val1: maxId,
                            val2: av.Id);
                    }

                    if (li.Index is VReg iv)
                    {
                        maxId = Math.Max(
                            val1: maxId,
                            val2: iv.Id);
                    }

                    break;
                case StoreIndex si:
                    if (si.Arr is VReg av2)
                    {
                        maxId = Math.Max(
                            val1: maxId,
                            val2: av2.Id);
                    }

                    if (si.Index is VReg iv2)
                    {
                        maxId = Math.Max(
                            val1: maxId,
                            val2: iv2.Id);
                    }

                    if (si.Value is VReg vv2)
                    {
                        maxId = Math.Max(
                            val1: maxId,
                            val2: vv2.Id);
                    }

                    break;
                case Call cl:
                    if (cl.Dst is { } cd)
                    {
                        maxId = Math.Max(
                            val1: maxId,
                            val2: cd.Id);
                    }

                    foreach (MOperand a in cl.Args)
                    {
                        if (a is VReg ra)
                        {
                            maxId = Math.Max(
                                val1: maxId,
                                val2: ra.Id);
                        }
                    }

                    break;
            }
        }

        var dm = new DynamicMethod(
            name: $"ciljit_{func.Name}",
            returnType: typeof(Value),
            parameterTypes: new[] { typeof(VmJitContext), typeof(Value[]) },
            m: typeof(CilEmitter).Module,
            skipVisibility: true);

        ILGenerator il = dm.GetILGenerator();

        LocalBuilder locals = il.DeclareLocal(typeof(Value[]));
        LocalBuilder tmp = il.DeclareLocal(typeof(Value));
        il.Emit(
            opcode: OpCodes.Ldc_I4,
            arg: Math.Max(
                val1: 1,
                val2: maxId + 1));

        il.Emit(
            opcode: OpCodes.Newarr,
            cls: typeof(Value));

        il.Emit(
            opcode: OpCodes.Stloc,
            local: locals);

        // Register locals as GC roots for the duration of this function: ctx.EnterFrame(locals)
        il.Emit(OpCodes.Ldarg_0); // ctx
        il.Emit(
            opcode: OpCodes.Ldloc,
            local: locals);

        il.Emit(
            opcode: OpCodes.Callvirt,
            meth: MiEnter);

        // Bind args to param regs
        for (int i = 0; i < func.ParamRegs.Count; i++)
        {
            il.Emit(
                opcode: OpCodes.Ldloc,
                local: locals);

            il.Emit(
                opcode: OpCodes.Ldc_I4,
                arg: func.ParamRegs[i].Id);

            il.Emit(OpCodes.Ldarg_1);
            il.Emit(
                opcode: OpCodes.Ldc_I4,
                arg: i);

            il.Emit(
                opcode: OpCodes.Call,
                meth: MiGet);

            il.Emit(
                opcode: OpCodes.Call,
                meth: MiSet);
        }

        // Labels
        Dictionary<MirBlock, Label> labels = func.Blocks.ToDictionary(
            keySelector: b => b,
            elementSelector: _ => il.DefineLabel());

        // Emit blocks
        foreach (MirBlock b in func.Blocks)
        {
            il.MarkLabel(labels[b]);

            foreach (MirInstr ins in b.Instructions)
            {
                switch (ins)
                {
                    case Move mv:
                        EmitLoadOperand(
                            il: il,
                            locals: locals,
                            op: mv.Src);

                        EmitStoreReg(
                            il: il,
                            locals: locals,
                            tmp: tmp,
                            id: mv.Dst.Id);

                        break;

                    case Bin bi:
                        EmitLoadOperand(
                            il: il,
                            locals: locals,
                            op: bi.L);

                        EmitLoadOperand(
                            il: il,
                            locals: locals,
                            op: bi.R);

                        if (bi.Op is MBinOp.Add or MBinOp.Sub or MBinOp.Mul or MBinOp.Div or MBinOp.Mod)
                        {
                            LocalBuilder r = il.DeclareLocal(typeof(Value));
                            LocalBuilder l = il.DeclareLocal(typeof(Value));
                            il.Emit(
                                opcode: OpCodes.Stloc,
                                local: r);

                            il.Emit(
                                opcode: OpCodes.Stloc,
                                local: l);

                            il.Emit(
                                opcode: OpCodes.Ldloc,
                                local: l);

                            il.Emit(
                                opcode: OpCodes.Call,
                                meth: MiI64);

                            il.Emit(
                                opcode: OpCodes.Ldloc,
                                local: r);

                            il.Emit(
                                opcode: OpCodes.Call,
                                meth: MiI64);

                            il.Emit(
                                bi.Op switch
                                {
                                    MBinOp.Add => OpCodes.Add,
                                    MBinOp.Sub => OpCodes.Sub,
                                    MBinOp.Mul => OpCodes.Mul,
                                    MBinOp.Div => OpCodes.Div,
                                    MBinOp.Mod => OpCodes.Rem,
                                    _ => throw new NotSupportedException()
                                });

                            il.Emit(
                                opcode: OpCodes.Call,
                                meth: MiFromLong);
                        }
                        else if (bi.Op is MBinOp.Eq or MBinOp.Ne)
                        {
                            LocalBuilder r = il.DeclareLocal(typeof(Value));
                            LocalBuilder l = il.DeclareLocal(typeof(Value));
                            il.Emit(
                                opcode: OpCodes.Stloc,
                                local: r);

                            il.Emit(
                                opcode: OpCodes.Stloc,
                                local: l);

                            il.Emit(
                                opcode: OpCodes.Ldloc,
                                local: l);

                            il.Emit(
                                opcode: OpCodes.Ldloc,
                                local: r);

                            il.Emit(
                                opcode: OpCodes.Call,
                                meth: MiEq);

                            if (bi.Op == MBinOp.Ne)
                            {
                                il.Emit(OpCodes.Ldc_I4_0);
                                il.Emit(OpCodes.Ceq);
                            }

                            il.Emit(
                                opcode: OpCodes.Call,
                                meth: MiFromBool);
                        }
                        else // relational (lt/le/gt/ge)
                        {
                            LocalBuilder r = il.DeclareLocal(typeof(Value));
                            LocalBuilder l = il.DeclareLocal(typeof(Value));
                            il.Emit(
                                opcode: OpCodes.Stloc,
                                local: r);

                            il.Emit(
                                opcode: OpCodes.Stloc,
                                local: l);

                            il.Emit(
                                opcode: OpCodes.Ldloc,
                                local: l);

                            il.Emit(
                                opcode: OpCodes.Call,
                                meth: MiI64);

                            il.Emit(
                                opcode: OpCodes.Ldloc,
                                local: r);

                            il.Emit(
                                opcode: OpCodes.Call,
                                meth: MiI64);

                            switch (bi.Op)
                            {
                                case MBinOp.Lt:
                                    il.Emit(OpCodes.Clt);

                                    break;
                                case MBinOp.Gt:
                                    il.Emit(OpCodes.Cgt);

                                    break;
                                case MBinOp.Le:
                                    il.Emit(OpCodes.Cgt);
                                    il.Emit(OpCodes.Ldc_I4_0);
                                    il.Emit(OpCodes.Ceq);

                                    break;
                                case MBinOp.Ge:
                                    il.Emit(OpCodes.Clt);
                                    il.Emit(OpCodes.Ldc_I4_0);
                                    il.Emit(OpCodes.Ceq);

                                    break;
                            }

                            il.Emit(
                                opcode: OpCodes.Call,
                                meth: MiFromBool);
                        }

                        EmitStoreReg(
                            il: il,
                            locals: locals,
                            tmp: tmp,
                            id: bi.Dst.Id);

                        break;

                    case Un un:
                        EmitLoadOperand(
                            il: il,
                            locals: locals,
                            op: un.X);

                        if (un.Op == MUnOp.Neg)
                        {
                            LocalBuilder x = il.DeclareLocal(typeof(Value));
                            il.Emit(
                                opcode: OpCodes.Stloc,
                                local: x);

                            il.Emit(
                                opcode: OpCodes.Ldloc,
                                local: x);

                            il.Emit(
                                opcode: OpCodes.Call,
                                meth: MiI64);

                            il.Emit(OpCodes.Neg);
                            il.Emit(
                                opcode: OpCodes.Call,
                                meth: MiFromLong);
                        }
                        else if (un.Op == MUnOp.Not)
                        {
                            il.Emit(
                                opcode: OpCodes.Call,
                                meth: MiToBool);

                            il.Emit(OpCodes.Ldc_I4_0);
                            il.Emit(OpCodes.Ceq);
                            il.Emit(
                                opcode: OpCodes.Call,
                                meth: MiFromBool);
                        }
                        else // Plus
                        {
                            LocalBuilder x = il.DeclareLocal(typeof(Value));
                            il.Emit(
                                opcode: OpCodes.Stloc,
                                local: x);

                            il.Emit(
                                opcode: OpCodes.Ldloc,
                                local: x);

                            il.Emit(
                                opcode: OpCodes.Call,
                                meth: MiI64);

                            il.Emit(
                                opcode: OpCodes.Call,
                                meth: MiFromLong);
                        }

                        EmitStoreReg(
                            il: il,
                            locals: locals,
                            tmp: tmp,
                            id: un.Dst.Id);

                        break;

                    case LoadIndex li:
                        EmitLoadOperand(
                            il: il,
                            locals: locals,
                            op: li.Arr);

                        EmitLoadOperand(
                            il: il,
                            locals: locals,
                            op: li.Index);

                    {
                        LocalBuilder idx = il.DeclareLocal(typeof(Value));
                        LocalBuilder arr = il.DeclareLocal(typeof(Value));
                        il.Emit(
                            opcode: OpCodes.Stloc,
                            local: idx);

                        il.Emit(
                            opcode: OpCodes.Stloc,
                            local: arr);

                        il.Emit(
                            opcode: OpCodes.Ldloc,
                            local: arr);

                        il.Emit(
                            opcode: OpCodes.Call,
                            meth: MiArr);

                        il.Emit(
                            opcode: OpCodes.Ldloc,
                            local: idx);

                        il.Emit(
                            opcode: OpCodes.Call,
                            meth: MiI64);

                        il.Emit(OpCodes.Conv_I4);
                        il.Emit(
                            opcode: OpCodes.Callvirt,
                            meth: typeof(VmArray).GetProperty("Item")!.GetMethod!);
                    }

                        EmitStoreReg(
                            il: il,
                            locals: locals,
                            tmp: tmp,
                            id: li.Dst.Id);

                        break;

                    case StoreIndex si:
                        EmitLoadOperand(
                            il: il,
                            locals: locals,
                            op: si.Arr);

                        EmitLoadOperand(
                            il: il,
                            locals: locals,
                            op: si.Index);

                        EmitLoadOperand(
                            il: il,
                            locals: locals,
                            op: si.Value);

                    {
                        LocalBuilder val = il.DeclareLocal(typeof(Value));
                        LocalBuilder idx = il.DeclareLocal(typeof(Value));
                        LocalBuilder arr = il.DeclareLocal(typeof(Value));
                        il.Emit(
                            opcode: OpCodes.Stloc,
                            local: val);

                        il.Emit(
                            opcode: OpCodes.Stloc,
                            local: idx);

                        il.Emit(
                            opcode: OpCodes.Stloc,
                            local: arr);

                        il.Emit(
                            opcode: OpCodes.Ldloc,
                            local: arr);

                        il.Emit(
                            opcode: OpCodes.Call,
                            meth: MiArr);

                        il.Emit(
                            opcode: OpCodes.Ldloc,
                            local: idx);

                        il.Emit(
                            opcode: OpCodes.Call,
                            meth: MiI64);

                        il.Emit(OpCodes.Conv_I4);
                        il.Emit(
                            opcode: OpCodes.Ldloc,
                            local: val);

                        il.Emit(
                            opcode: OpCodes.Callvirt,
                            meth: typeof(VmArray).GetProperty("Item")!.SetMethod!);
                    }

                        break;

                    case Call cl:
                        {
                            int argc = cl.Args.Count;
                            LocalBuilder argsArr = il.DeclareLocal(typeof(Value[]));
                            il.Emit(
                                opcode: OpCodes.Ldc_I4,
                                arg: argc);

                            il.Emit(
                                opcode: OpCodes.Newarr,
                                cls: typeof(Value));

                            il.Emit(
                                opcode: OpCodes.Stloc,
                                local: argsArr);

                            for (int i = 0; i < argc; i++)
                            {
                                il.Emit(
                                    opcode: OpCodes.Ldloc,
                                    local: argsArr);

                                il.Emit(
                                    opcode: OpCodes.Ldc_I4,
                                    arg: i);

                                EmitLoadOperand(
                                    il: il,
                                    locals: locals,
                                    op: cl.Args[i]);

                                il.Emit(
                                    opcode: OpCodes.Stelem,
                                    cls: typeof(Value));
                            }

                            string callee = cl.Callee;

                            if (IsBuiltinName(callee))
                            {
                                // BuiltinsVm.Invoke(callee, ctx, args)
                                il.Emit(
                                    opcode: OpCodes.Ldstr,
                                    str: callee);

                                il.Emit(OpCodes.Ldarg_0);
                                il.Emit(
                                    opcode: OpCodes.Ldloc,
                                    local: argsArr);

                                il.Emit(
                                    opcode: OpCodes.Call,
                                    meth: MiBuiltins);
                            }
                            else
                            {
                                // ctx.InvokeFunction(callee, args)
                                il.Emit(OpCodes.Ldarg_0);
                                il.Emit(
                                    opcode: OpCodes.Ldstr,
                                    str: callee);

                                il.Emit(
                                    opcode: OpCodes.Ldloc,
                                    local: argsArr);

                                il.Emit(
                                    opcode: OpCodes.Callvirt,
                                    meth: MiInvokeFn);
                            }

                            if (cl.Dst is { } dst)
                            {
                                EmitStoreReg(
                                    il: il,
                                    locals: locals,
                                    tmp: tmp,
                                    id: dst.Id);
                            }

                            break;
                        }

                    default:
                        throw new NotSupportedException(
                            ins.GetType()
                                .Name);
                }
            }

            // Terminator
            switch (b.Terminator)
            {
                case null:
                    break;
                case Ret r:
                    if (r.Value is null)
                    {
                        il.Emit(
                            opcode: OpCodes.Ldsfld,
                            field: FiNull);
                    }
                    else
                    {
                        EmitLoadOperand(
                            il: il,
                            locals: locals,
                            op: r.Value);
                    }

                    // Pop GC roots frame: ctx.ExitFrame()
                    il.Emit(OpCodes.Ldarg_0);
                    il.Emit(
                        opcode: OpCodes.Callvirt,
                        meth: MiExit);

                    il.Emit(OpCodes.Ret);

                    break;

                case Br br:
                    il.Emit(
                        opcode: OpCodes.Br,
                        label: labels[br.Target]);

                    break;

                case BrCond bc:
                    EmitLoadOperand(
                        il: il,
                        locals: locals,
                        op: bc.Cond);

                    il.Emit(
                        opcode: OpCodes.Call,
                        meth: MiToBool);

                    LocalBuilder ctmp = il.DeclareLocal(typeof(bool));
                    il.Emit(
                        opcode: OpCodes.Stloc,
                        local: ctmp);

                    il.Emit(
                        opcode: OpCodes.Ldloc,
                        local: ctmp);

                    il.Emit(
                        opcode: OpCodes.Brtrue,
                        label: labels[bc.IfTrue]);

                    il.Emit(
                        opcode: OpCodes.Br,
                        label: labels[bc.IfFalse]);

                    break;

                default:
                    throw new NotSupportedException(
                        b.Terminator.GetType()
                            .Name);
            }
        }

        // default return null
        il.Emit(
            opcode: OpCodes.Ldsfld,
            field: FiNull);

        // Pop GC roots frame (in default path)
        il.Emit(OpCodes.Ldarg_0);
        il.Emit(
            opcode: OpCodes.Callvirt,
            meth: MiExit);

        il.Emit(OpCodes.Ret);

        return (VmJitFunc)dm.CreateDelegate(typeof(VmJitFunc));
    }

    private static void EmitLoadOperand(
        ILGenerator il,
        LocalBuilder locals,
        MOperand op)
    {
        switch (op)
        {
            case Const c:
                if (c.Value is null)
                {
                    il.Emit(
                        opcode: OpCodes.Ldsfld,
                        field: FiNull);
                }
                else if (c.Value is long ln)
                {
                    il.Emit(
                        opcode: OpCodes.Ldc_I8,
                        arg: ln);

                    il.Emit(
                        opcode: OpCodes.Call,
                        meth: MiFromLong);
                }
                else if (c.Value is bool bb)
                {
                    il.Emit(
                        bb
                            ? OpCodes.Ldc_I4_1
                            : OpCodes.Ldc_I4_0);

                    il.Emit(
                        opcode: OpCodes.Call,
                        meth: MiFromBool);
                }
                else if (c.Value is char ch)
                {
                    il.Emit(
                        opcode: OpCodes.Ldc_I4,
                        arg: ch);

                    il.Emit(
                        opcode: OpCodes.Call,
                        meth: MiFromChar);
                }
                else if (c.Value is string s)
                {
                    il.Emit(
                        opcode: OpCodes.Ldstr,
                        str: s);

                    il.Emit(
                        opcode: OpCodes.Call,
                        meth: MiFromString);
                }
                else
                {
                    throw new NotSupportedException($"const {c.Value?.GetType().Name}");
                }

                break;
            case VReg v:
                il.Emit(
                    opcode: OpCodes.Ldloc,
                    local: locals);

                il.Emit(
                    opcode: OpCodes.Ldc_I4,
                    arg: v.Id);

                il.Emit(
                    opcode: OpCodes.Call,
                    meth: MiGet);

                break;
            default:
                throw new NotSupportedException(
                    op.GetType()
                        .Name);
        }
    }

    private static void EmitStoreReg(
        ILGenerator il,
        LocalBuilder locals,
        LocalBuilder tmp,
        int id)
    {
        il.Emit(
            opcode: OpCodes.Stloc,
            local: tmp);

        il.Emit(
            opcode: OpCodes.Ldloc,
            local: locals);

        il.Emit(
            opcode: OpCodes.Ldc_I4,
            arg: id);

        il.Emit(
            opcode: OpCodes.Ldloc,
            local: tmp);

        il.Emit(
            opcode: OpCodes.Call,
            meth: MiSet);
    }

    private static bool IsBuiltinName(
        string name)
    {
        return Builtins.Exists(name);
    }

    internal sealed class CilModule
    {
        public CilModule(
            IReadOnlyDictionary<string, VmJitFunc> functions)
        {
            Functions = functions;
        }

        public IReadOnlyDictionary<string, VmJitFunc> Functions { get; }
    }
}
