using System.Reflection.Emit;

using Compiler.Frontend.Translation.MIR.Common;
using Compiler.Frontend.Translation.MIR.Instructions;
using Compiler.Frontend.Translation.MIR.Instructions.Abstractions;
using Compiler.Frontend.Translation.MIR.Operands;

namespace Compiler.Backend.CLR;

public sealed partial class CilBackend
{
    private void EmitMove(
        ILGenerator il,
        MirFunction f,
        Move mv,
        Func<VReg, LocalBuilder> getLocal)
    {
        var dstT = GetClrTypeForOperand(f, mv.Dst);
        if (dstT == typeof(object))
            EmitLoadAsObject(il, f, mv.Src, getLocal);
        else
            EmitLoadRaw(il, f, mv.Src, dstT, getLocal);
        il.Emit(OpCodes.Stloc, getLocal(mv.Dst));
    }

    private void EmitBin(
        ILGenerator il,
        MirFunction f,
        Bin bi,
        Func<VReg, LocalBuilder> getLocal)
    {
        var dstT = GetClrTypeForOperand(f, bi.Dst);
        bool isI64Arith = bi.Op is MBinOp.Add or MBinOp.Sub or MBinOp.Mul or MBinOp.Div or MBinOp.Mod;
        bool isCmp      = bi.Op is MBinOp.Lt or MBinOp.Le or MBinOp.Gt or MBinOp.Ge or MBinOp.Eq or MBinOp.Ne;

        if (dstT == typeof(long) && isI64Arith)
        {
            EmitLoadRaw(il, f, bi.L, typeof(long), getLocal);
            EmitLoadRaw(il, f, bi.R, typeof(long), getLocal);
            il.Emit(bi.Op switch
            {
                MBinOp.Add => OpCodes.Add,
                MBinOp.Sub => OpCodes.Sub,
                MBinOp.Mul => OpCodes.Mul,
                MBinOp.Div => OpCodes.Div,
                MBinOp.Mod => OpCodes.Rem,
                _ => throw new NotSupportedException()
            });
            il.Emit(OpCodes.Stloc, getLocal(bi.Dst));
            return;
        }

        if (dstT == typeof(bool) && isCmp)
        {
            // Сравнение по i64; загрузчик сам сделает unbox/conv при необходимости
            EmitLoadRaw(il, f, bi.L, typeof(long), getLocal);
            EmitLoadRaw(il, f, bi.R, typeof(long), getLocal);
            switch (bi.Op)
            {
                case MBinOp.Eq: il.Emit(OpCodes.Ceq); break;
                case MBinOp.Ne: il.Emit(OpCodes.Ceq); il.Emit(OpCodes.Ldc_I4_0); il.Emit(OpCodes.Ceq); break;
                case MBinOp.Lt: il.Emit(OpCodes.Clt); break;
                case MBinOp.Gt: il.Emit(OpCodes.Cgt); break;
                case MBinOp.Le: il.Emit(OpCodes.Cgt); il.Emit(OpCodes.Ldc_I4_0); il.Emit(OpCodes.Ceq); break;
                case MBinOp.Ge: il.Emit(OpCodes.Clt); il.Emit(OpCodes.Ldc_I4_0); il.Emit(OpCodes.Ceq); break;
            }
            il.Emit(OpCodes.Stloc, getLocal(bi.Dst));
            return;
        }

        // boxed fallback
        EmitLoadAsObject(il, f, bi.L, getLocal);
        EmitLoadAsObject(il, f, bi.R, getLocal);
        il.Emit(OpCodes.Call, MapBin(bi.Op));
        il.Emit(OpCodes.Stloc, getLocal(bi.Dst));
    }

    private void EmitUn(
        ILGenerator il,
        MirFunction f,
        Un un,
        Func<VReg, LocalBuilder> getLocal)
    {
        var dstT = GetClrTypeForOperand(f, un.Dst);
        if (dstT == typeof(long) && (un.Op is MUnOp.Neg or MUnOp.Plus))
        {
            EmitLoadRaw(il, f, un.X, typeof(long), getLocal);
            if (un.Op == MUnOp.Neg) il.Emit(OpCodes.Neg);
            il.Emit(OpCodes.Stloc, getLocal(un.Dst));
            return;
        }
        if (dstT == typeof(bool) && un.Op == MUnOp.Not)
        {
            EmitLoadRaw(il, f, un.X, typeof(bool), getLocal);
            il.Emit(OpCodes.Ldc_I4_0);
            il.Emit(OpCodes.Ceq);
            il.Emit(OpCodes.Stloc, getLocal(un.Dst));
            return;
        }
        // boxed fallback
        EmitLoadAsObject(il, f, un.X, getLocal);
        il.Emit(OpCodes.Call, MapUn(un.Op));
        il.Emit(OpCodes.Stloc, getLocal(un.Dst));
    }

    private void EmitLoadIndex(
        ILGenerator il,
        MirFunction f,
        LoadIndex li,
        Func<VReg, LocalBuilder> getLocal)
    {
        EmitLoadAsObject(il, f, li.Arr, getLocal);
        EmitLoadAsObject(il, f, li.Index, getLocal);
        il.Emit(OpCodes.Call, _rt["LoadIndex"]);
        var dstT = GetClrTypeForOperand(f, li.Dst);
        if (dstT == typeof(object))
            il.Emit(OpCodes.Stloc, getLocal(li.Dst));
        else
        {
            il.Emit(OpCodes.Unbox_Any, dstT);
            il.Emit(OpCodes.Stloc, getLocal(li.Dst));
        }
    }

    private void EmitStoreIndex(
        ILGenerator il,
        MirFunction f,
        StoreIndex si,
        Func<VReg, LocalBuilder> getLocal)
    {
        EmitLoadAsObject(il, f, si.Arr, getLocal);
        EmitLoadAsObject(il, f, si.Index, getLocal);
        EmitLoadAsObject(il, f, si.Value, getLocal);
        il.Emit(OpCodes.Call, _rt["StoreIndex"]);
    }

    private void EmitCall(
        ILGenerator il,
        MirFunction f,
        Call cl,
        Func<VReg, LocalBuilder> getLocal,
        Dictionary<string, MethodBuilder> methods,
        HashSet<string> funcNames)
    {
        EmitArgsArray(il, f, cl.Args, getLocal);
        var tmpArgs = il.DeclareLocal(typeof(object[]));
        il.Emit(OpCodes.Stloc, tmpArgs);

        if (funcNames.Contains(cl.Callee))
        {
            MethodBuilder mi = methods[cl.Callee];
            il.Emit(OpCodes.Ldloc, tmpArgs);
            il.Emit(OpCodes.Call, mi);
        }
        else
        {
            il.Emit(OpCodes.Ldstr, cl.Callee);
            il.Emit(OpCodes.Ldloc, tmpArgs);
            il.Emit(OpCodes.Call, BuiltinsInvoke);
        }

        if (cl.Dst is not null)
        {
            var dstT = GetClrTypeForOperand(f, cl.Dst);
            if (dstT == typeof(object))
                il.Emit(OpCodes.Stloc, getLocal(cl.Dst));
            else
            {
                il.Emit(OpCodes.Unbox_Any, dstT);
                il.Emit(OpCodes.Stloc, getLocal(cl.Dst));
            }
        }
        else
        {
            il.Emit(OpCodes.Pop);
        }
    }
}
