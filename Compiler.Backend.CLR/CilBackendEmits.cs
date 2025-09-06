using System.Reflection.Emit;

using Compiler.Frontend.Translation.MIR.Common;
using Compiler.Frontend.Translation.MIR.Instructions;
using Compiler.Frontend.Translation.MIR.Instructions.Abstractions;
using Compiler.Frontend.Translation.MIR.Operands;

namespace Compiler.Backend.CLR;

public sealed partial class CilBackend
{
    private void EmitBin(
        ILGenerator il,
        MirFunction f,
        Bin bi,
        Func<VReg, LocalBuilder> getLocal)
    {
        Type dstT = GetClrTypeForOperand(
            f: f,
            v: bi.Dst);

        bool isI64Arith = bi.Op is MBinOp.Add or MBinOp.Sub or MBinOp.Mul or MBinOp.Div or MBinOp.Mod;
        bool isCmp = bi.Op is MBinOp.Lt or MBinOp.Le or MBinOp.Gt or MBinOp.Ge or MBinOp.Eq or MBinOp.Ne;

        if (dstT == typeof(long) && isI64Arith)
        {
            EmitLoadRaw(
                il: il,
                f: f,
                op: bi.L,
                expected: typeof(long),
                getLocal: getLocal);

            EmitLoadRaw(
                il: il,
                f: f,
                op: bi.R,
                expected: typeof(long),
                getLocal: getLocal);

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
                opcode: OpCodes.Stloc,
                local: getLocal(bi.Dst));

            return;
        }

        if (dstT == typeof(bool) && isCmp)
        {
            // Compare as i64; loader will unbox/convert if needed
            EmitLoadRaw(
                il: il,
                f: f,
                op: bi.L,
                expected: typeof(long),
                getLocal: getLocal);

            EmitLoadRaw(
                il: il,
                f: f,
                op: bi.R,
                expected: typeof(long),
                getLocal: getLocal);

            switch (bi.Op)
            {
                case MBinOp.Eq:
                    il.Emit(OpCodes.Ceq);

                    break;
                case MBinOp.Ne:
                    il.Emit(OpCodes.Ceq);
                    il.Emit(OpCodes.Ldc_I4_0);
                    il.Emit(OpCodes.Ceq);

                    break;
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
                opcode: OpCodes.Stloc,
                local: getLocal(bi.Dst));

            return;
        }

        // boxed fallback
        EmitLoadAsObject(
            il: il,
            f: f,
            op: bi.L,
            getLocal: getLocal);

        EmitLoadAsObject(
            il: il,
            f: f,
            op: bi.R,
            getLocal: getLocal);

        il.Emit(
            opcode: OpCodes.Call,
            meth: MapBin(bi.Op));

        il.Emit(
            opcode: OpCodes.Stloc,
            local: getLocal(bi.Dst));
    }

    private void EmitCall(
        ILGenerator il,
        MirFunction f,
        Call cl,
        Func<VReg, LocalBuilder> getLocal,
        Dictionary<string, MethodBuilder> methods,
        HashSet<string> funcNames)
    {
        EmitArgsArray(
            ilgen: il,
            func: f,
            args: cl.Args,
            getLocal: getLocal);

        LocalBuilder tmpArgs = il.DeclareLocal(typeof(object[]));
        il.Emit(
            opcode: OpCodes.Stloc,
            local: tmpArgs);

        if (funcNames.Contains(cl.Callee))
        {
            MethodBuilder mi = methods[cl.Callee];
            il.Emit(
                opcode: OpCodes.Ldloc,
                local: tmpArgs);

            il.Emit(
                opcode: OpCodes.Call,
                meth: mi);
        }
        else
        {
            il.Emit(
                opcode: OpCodes.Ldstr,
                str: cl.Callee);

            il.Emit(
                opcode: OpCodes.Ldloc,
                local: tmpArgs);

            il.Emit(
                opcode: OpCodes.Call,
                meth: BuiltinsInvoke);
        }

        if (cl.Dst is not null)
        {
            Type dstT = GetClrTypeForOperand(
                f: f,
                v: cl.Dst);

            if (dstT == typeof(object))
            {
                il.Emit(
                    opcode: OpCodes.Stloc,
                    local: getLocal(cl.Dst));
            }
            else
            {
                il.Emit(
                    opcode: OpCodes.Unbox_Any,
                    cls: dstT);

                il.Emit(
                    opcode: OpCodes.Stloc,
                    local: getLocal(cl.Dst));
            }
        }
        else
        {
            il.Emit(OpCodes.Pop);
        }
    }

    private void EmitLoadIndex(
        ILGenerator il,
        MirFunction f,
        LoadIndex li,
        Func<VReg, LocalBuilder> getLocal)
    {
        EmitLoadAsObject(
            il: il,
            f: f,
            op: li.Arr,
            getLocal: getLocal);

        EmitLoadAsObject(
            il: il,
            f: f,
            op: li.Index,
            getLocal: getLocal);

        il.Emit(
            opcode: OpCodes.Call,
            meth: _rt["LoadIndex"]);

        Type dstT = GetClrTypeForOperand(
            f: f,
            v: li.Dst);

        if (dstT == typeof(object))
        {
            il.Emit(
                opcode: OpCodes.Stloc,
                local: getLocal(li.Dst));
        }
        else
        {
            il.Emit(
                opcode: OpCodes.Unbox_Any,
                cls: dstT);

            il.Emit(
                opcode: OpCodes.Stloc,
                local: getLocal(li.Dst));
        }
    }
    private void EmitMove(
        ILGenerator il,
        MirFunction f,
        Move mv,
        Func<VReg, LocalBuilder> getLocal)
    {
        Type dstT = GetClrTypeForOperand(
            f: f,
            v: mv.Dst);

        if (dstT == typeof(object))
        {
            EmitLoadAsObject(
                il: il,
                f: f,
                op: mv.Src,
                getLocal: getLocal);
        }
        else
        {
            EmitLoadRaw(
                il: il,
                f: f,
                op: mv.Src,
                expected: dstT,
                getLocal: getLocal);
        }

        il.Emit(
            opcode: OpCodes.Stloc,
            local: getLocal(mv.Dst));
    }

    private void EmitStoreIndex(
        ILGenerator il,
        MirFunction f,
        StoreIndex si,
        Func<VReg, LocalBuilder> getLocal)
    {
        EmitLoadAsObject(
            il: il,
            f: f,
            op: si.Arr,
            getLocal: getLocal);

        EmitLoadAsObject(
            il: il,
            f: f,
            op: si.Index,
            getLocal: getLocal);

        EmitLoadAsObject(
            il: il,
            f: f,
            op: si.Value,
            getLocal: getLocal);

        il.Emit(
            opcode: OpCodes.Call,
            meth: _rt["StoreIndex"]);
    }

    private void EmitUn(
        ILGenerator il,
        MirFunction f,
        Un un,
        Func<VReg, LocalBuilder> getLocal)
    {
        Type dstT = GetClrTypeForOperand(
            f: f,
            v: un.Dst);

        if (dstT == typeof(long) && un.Op is MUnOp.Neg or MUnOp.Plus)
        {
            EmitLoadRaw(
                il: il,
                f: f,
                op: un.X,
                expected: typeof(long),
                getLocal: getLocal);

            if (un.Op == MUnOp.Neg)
            {
                il.Emit(OpCodes.Neg);
            }

            il.Emit(
                opcode: OpCodes.Stloc,
                local: getLocal(un.Dst));

            return;
        }

        if (dstT == typeof(bool) && un.Op == MUnOp.Not)
        {
            EmitLoadRaw(
                il: il,
                f: f,
                op: un.X,
                expected: typeof(bool),
                getLocal: getLocal);

            il.Emit(OpCodes.Ldc_I4_0);
            il.Emit(OpCodes.Ceq);
            il.Emit(
                opcode: OpCodes.Stloc,
                local: getLocal(un.Dst));

            return;
        }

        // boxed fallback
        EmitLoadAsObject(
            il: il,
            f: f,
            op: un.X,
            getLocal: getLocal);

        il.Emit(
            opcode: OpCodes.Call,
            meth: MapUn(un.Op));

        il.Emit(
            opcode: OpCodes.Stloc,
            local: getLocal(un.Dst));
    }
}
