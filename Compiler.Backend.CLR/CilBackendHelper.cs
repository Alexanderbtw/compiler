using System.Reflection;
using System.Reflection.Emit;

using Compiler.Frontend.Translation.MIR.Common;
using Compiler.Frontend.Translation.MIR.Instructions.Abstractions;
using Compiler.Frontend.Translation.MIR.Operands;

namespace Compiler.Backend.CLR;

public sealed partial class CilBackend
{
    // === Type mapping helpers ===
    private static Type GetClrTypeForVReg(MirFunction func, VReg v)
    {
        if (func.Types.TryGetValue(v.Id, out MirFunction.MType mt))
        {
            return mt switch
            {
                MirFunction.MType.I64  => typeof(long),
                MirFunction.MType.Bool => typeof(bool),
                MirFunction.MType.Char => typeof(char),
                _                      => typeof(object)
            };
        }
        return typeof(object);
    }

    private static Type GetClrTypeForOperand(MirFunction f, VReg v)
    {
        if (f.Types.TryGetValue(v.Id, out MirFunction.MType mt))
        {
            return mt switch
            {
                MirFunction.MType.I64  => typeof(long),
                MirFunction.MType.Bool => typeof(bool),
                MirFunction.MType.Char => typeof(char),
                _                      => typeof(object)
            };
        }
        return typeof(object);
    }

    // === Constants & operands ===
    private static void EmitConst(ILGenerator il, object? val)
    {
        switch (val)
        {
            case null:    il.Emit(OpCodes.Ldnull); break;
            case string s: il.Emit(OpCodes.Ldstr, s); break;
            case long n:
                il.Emit(OpCodes.Ldc_I8, n);
                il.Emit(OpCodes.Box, typeof(long));
                break;
            case bool b:
                il.Emit(b ? OpCodes.Ldc_I4_1 : OpCodes.Ldc_I4_0);
                il.Emit(OpCodes.Box, typeof(bool));
                break;
            case char ch:
                il.Emit(OpCodes.Ldc_I4, ch);
                il.Emit(OpCodes.Box, typeof(char));
                break;
            default:
                throw new NotSupportedException($"Const {val?.GetType().Name}");
        }
    }

    private void EmitOperand(ILGenerator il, MOperand? op, Func<VReg, LocalBuilder> getLocal)
    {
        switch (op)
        {
            case null:
                il.Emit(OpCodes.Ldnull);
                return;
            case VReg v:
                il.Emit(OpCodes.Ldloc, getLocal(v));
                return;
            case Const c:
                EmitConst(il, c.Value);
                return;
            default:
                throw new NotSupportedException($"Operand {op.GetType().Name}");
        }
    }

    private void EmitLoadAsObject(
        ILGenerator il,
        MirFunction f,
        MOperand op,
        Func<VReg, LocalBuilder> getLocal)
    {
        switch (op)
        {
            case Const c:
                EmitConst(il, c.Value);
                return;
            case VReg v:
                il.Emit(OpCodes.Ldloc, getLocal(v));
                var t = GetClrTypeForOperand(f, v);
                if (t == typeof(long)) il.Emit(OpCodes.Box, typeof(long));
                else if (t == typeof(bool)) il.Emit(OpCodes.Box, typeof(bool));
                else if (t == typeof(char)) il.Emit(OpCodes.Box, typeof(char));
                return;
            default:
                throw new NotSupportedException($"Operand {op.GetType().Name}");
        }
    }

    private void EmitLoadRaw(
        ILGenerator il,
        MirFunction f,
        MOperand op,
        Type expected,
        Func<VReg, LocalBuilder> getLocal)
    {
        switch (op)
        {
            case Const c:
                if (expected == typeof(long) && c.Value is long ln)
                    il.Emit(OpCodes.Ldc_I8, ln);
                else if (expected == typeof(bool) && c.Value is bool bb)
                    il.Emit(bb ? OpCodes.Ldc_I4_1 : OpCodes.Ldc_I4_0);
                else if (expected == typeof(char) && c.Value is char ch)
                    il.Emit(OpCodes.Ldc_I4, ch);
                else
                {
                    // Fallback: как object и затем unbox.any
                    EmitConst(il, c.Value);
                    il.Emit(OpCodes.Unbox_Any, expected);
                }
                return;

            case VReg v:
                il.Emit(OpCodes.Ldloc, getLocal(v));
                var t = GetClrTypeForOperand(f, v);
                if (t != expected)
                {
                    if (t == typeof(object)) il.Emit(OpCodes.Unbox_Any, expected);
                    else if (expected == typeof(long) && t == typeof(char)) il.Emit(OpCodes.Conv_I8);
                    else if (expected == typeof(long) && t == typeof(bool)) il.Emit(OpCodes.Conv_I8);
                }
                return;

            default:
                throw new NotSupportedException($"Operand {op.GetType().Name}");
        }
    }

    private void EmitArgsArray(
        ILGenerator ilgen,
        MirFunction func,
        IReadOnlyList<MOperand> args,
        Func<VReg, LocalBuilder> getLocal)
    {
        ilgen.Emit(OpCodes.Ldc_I4, args.Count);
        ilgen.Emit(OpCodes.Newarr, typeof(object));
        for (int i = 0; i < args.Count; i++)
        {
            ilgen.Emit(OpCodes.Dup);
            ilgen.Emit(OpCodes.Ldc_I4, i);
            EmitLoadAsObject(ilgen, func, args[i], getLocal);
            ilgen.Emit(OpCodes.Stelem_Ref);
        }
    }

    // === Runtime maps ===
    private MethodInfo MapBin(MBinOp op) => op switch
    {
        MBinOp.Add => _rt["Add"],
        MBinOp.Sub => _rt["Sub"],
        MBinOp.Mul => _rt["Mul"],
        MBinOp.Div => _rt["Div"],
        MBinOp.Mod => _rt["Mod"],
        MBinOp.Lt  => _rt["Lt"],
        MBinOp.Le  => _rt["Le"],
        MBinOp.Gt  => _rt["Gt"],
        MBinOp.Ge  => _rt["Ge"],
        MBinOp.Eq  => _rt["Eq"],
        MBinOp.Ne  => _rt["Ne"],
        _ => throw new NotSupportedException(op.ToString())
    };

    private MethodInfo MapUn(MUnOp op) => op switch
    {
        MUnOp.Neg  => _rt["Neg"],
        MUnOp.Not  => _rt["Not"],
        MUnOp.Plus => _rt["Plus"],
        _ => throw new NotSupportedException(op.ToString())
    };
}
