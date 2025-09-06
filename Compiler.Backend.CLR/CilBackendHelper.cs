using System.Reflection;
using System.Reflection.Emit;

using Compiler.Frontend.Translation.MIR.Common;
using Compiler.Frontend.Translation.MIR.Instructions.Abstractions;
using Compiler.Frontend.Translation.MIR.Operands;

namespace Compiler.Backend.CLR;

public sealed partial class CilBackend
{
    private void EmitArgsArray(
        ILGenerator ilgen,
        MirFunction func,
        IReadOnlyList<MOperand> args,
        Func<VReg, LocalBuilder> getLocal)
    {
        ilgen.Emit(
            opcode: OpCodes.Ldc_I4,
            arg: args.Count);

        ilgen.Emit(
            opcode: OpCodes.Newarr,
            cls: typeof(object));

        for (int i = 0; i < args.Count; i++)
        {
            ilgen.Emit(OpCodes.Dup);
            ilgen.Emit(
                opcode: OpCodes.Ldc_I4,
                arg: i);

            EmitLoadAsObject(
                il: ilgen,
                f: func,
                op: args[i],
                getLocal: getLocal);

            ilgen.Emit(OpCodes.Stelem_Ref);
        }
    }

    // === Constants & operands ===
    private static void EmitConst(
        ILGenerator il,
        object? val)
    {
        switch (val)
        {
            case null:
                il.Emit(OpCodes.Ldnull);

                break;
            case string s:
                il.Emit(
                    opcode: OpCodes.Ldstr,
                    str: s);

                break;
            case long n:
                il.Emit(
                    opcode: OpCodes.Ldc_I8,
                    arg: n);

                il.Emit(
                    opcode: OpCodes.Box,
                    cls: typeof(long));

                break;
            case bool b:
                il.Emit(
                    b
                        ? OpCodes.Ldc_I4_1
                        : OpCodes.Ldc_I4_0);

                il.Emit(
                    opcode: OpCodes.Box,
                    cls: typeof(bool));

                break;
            case char ch:
                il.Emit(
                    opcode: OpCodes.Ldc_I4,
                    arg: ch);

                il.Emit(
                    opcode: OpCodes.Box,
                    cls: typeof(char));

                break;
            default:
                throw new NotSupportedException($"Const {val?.GetType().Name}");
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
                EmitConst(
                    il: il,
                    val: c.Value);

                return;
            case VReg v:
                il.Emit(
                    opcode: OpCodes.Ldloc,
                    local: getLocal(v));

                Type t = GetClrTypeForOperand(
                    f: f,
                    v: v);

                if (t == typeof(long))
                {
                    il.Emit(
                        opcode: OpCodes.Box,
                        cls: typeof(long));
                }
                else if (t == typeof(bool))
                {
                    il.Emit(
                        opcode: OpCodes.Box,
                        cls: typeof(bool));
                }
                else if (t == typeof(char))
                {
                    il.Emit(
                        opcode: OpCodes.Box,
                        cls: typeof(char));
                }

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
                {
                    il.Emit(
                        opcode: OpCodes.Ldc_I8,
                        arg: ln);
                }
                else if (expected == typeof(bool) && c.Value is bool bb)
                {
                    il.Emit(
                        bb
                            ? OpCodes.Ldc_I4_1
                            : OpCodes.Ldc_I4_0);
                }
                else if (expected == typeof(char) && c.Value is char ch)
                {
                    il.Emit(
                        opcode: OpCodes.Ldc_I4,
                        arg: ch);
                }
                else
                {
                    // Fallback: load as object then unbox.any
                    EmitConst(
                        il: il,
                        val: c.Value);

                    il.Emit(
                        opcode: OpCodes.Unbox_Any,
                        cls: expected);
                }

                return;

            case VReg v:
                il.Emit(
                    opcode: OpCodes.Ldloc,
                    local: getLocal(v));

                Type t = GetClrTypeForOperand(
                    f: f,
                    v: v);

                if (t != expected)
                {
                    if (t == typeof(object))
                    {
                        il.Emit(
                            opcode: OpCodes.Unbox_Any,
                            cls: expected);
                    }
                    else if (expected == typeof(long) && t == typeof(char))
                    {
                        il.Emit(OpCodes.Conv_I8);
                    }
                    else if (expected == typeof(long) && t == typeof(bool))
                    {
                        il.Emit(OpCodes.Conv_I8);
                    }
                }

                return;

            default:
                throw new NotSupportedException($"Operand {op.GetType().Name}");
        }
    }

    private void EmitOperand(
        ILGenerator il,
        MOperand? op,
        Func<VReg, LocalBuilder> getLocal)
    {
        switch (op)
        {
            case null:
                il.Emit(OpCodes.Ldnull);

                return;
            case VReg v:
                il.Emit(
                    opcode: OpCodes.Ldloc,
                    local: getLocal(v));

                return;
            case Const c:
                EmitConst(
                    il: il,
                    val: c.Value);

                return;
            default:
                throw new NotSupportedException($"Operand {op.GetType().Name}");
        }
    }

    private static Type GetClrTypeForOperand(
        MirFunction f,
        VReg v)
    {
        if (f.Types.TryGetValue(
                key: v.Id,
                value: out MirFunction.MType mt))
        {
            return mt switch
            {
                MirFunction.MType.I64 => typeof(long),
                MirFunction.MType.Bool => typeof(bool),
                MirFunction.MType.Char => typeof(char),
                _ => typeof(object)
            };
        }

        return typeof(object);
    }

    // === Type mapping helpers ===
    private static Type GetClrTypeForVReg(
        MirFunction func,
        VReg v)
    {
        if (func.Types.TryGetValue(
                key: v.Id,
                value: out MirFunction.MType mt))
        {
            return mt switch
            {
                MirFunction.MType.I64 => typeof(long),
                MirFunction.MType.Bool => typeof(bool),
                MirFunction.MType.Char => typeof(char),
                _ => typeof(object)
            };
        }

        return typeof(object);
    }

    // === Runtime maps ===
    private MethodInfo MapBin(
        MBinOp op)
    {
        return op switch
        {
            MBinOp.Add => _rt["Add"],
            MBinOp.Sub => _rt["Sub"],
            MBinOp.Mul => _rt["Mul"],
            MBinOp.Div => _rt["Div"],
            MBinOp.Mod => _rt["Mod"],
            MBinOp.Lt => _rt["Lt"],
            MBinOp.Le => _rt["Le"],
            MBinOp.Gt => _rt["Gt"],
            MBinOp.Ge => _rt["Ge"],
            MBinOp.Eq => _rt["Eq"],
            MBinOp.Ne => _rt["Ne"],
            _ => throw new NotSupportedException(op.ToString())
        };
    }

    private MethodInfo MapUn(
        MUnOp op)
    {
        return op switch
        {
            MUnOp.Neg => _rt["Neg"],
            MUnOp.Not => _rt["Not"],
            MUnOp.Plus => _rt["Plus"],
            _ => throw new NotSupportedException(op.ToString())
        };
    }
}
