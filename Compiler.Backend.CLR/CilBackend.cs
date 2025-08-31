using System.Reflection;
using System.Reflection.Emit;

using Compiler.Backend.CLR.Runtime;
using Compiler.Translation.MIR.Common;
using Compiler.Translation.MIR.Instructions;
using Compiler.Translation.MIR.Instructions.Abstractions;
using Compiler.Translation.MIR.Operands;

namespace Compiler.Backend.CLR;

public sealed class CilBackend
{
    private readonly Dictionary<string, MethodInfo> _rt = new()
    {
        ["Add"] = typeof(Runtime.Runtime).GetMethod(nameof(Runtime.Runtime.Add))!,
        ["Sub"] = typeof(Runtime.Runtime).GetMethod(nameof(Runtime.Runtime.Sub))!,
        ["Mul"] = typeof(Runtime.Runtime).GetMethod(nameof(Runtime.Runtime.Mul))!,
        ["Div"] = typeof(Runtime.Runtime).GetMethod(nameof(Runtime.Runtime.Div))!,
        ["Mod"] = typeof(Runtime.Runtime).GetMethod(nameof(Runtime.Runtime.Mod))!,
        ["Neg"] = typeof(Runtime.Runtime).GetMethod(nameof(Runtime.Runtime.Neg))!,
        ["Plus"] = typeof(Runtime.Runtime).GetMethod(nameof(Runtime.Runtime.Plus))!,
        ["Not"] = typeof(Runtime.Runtime).GetMethod(nameof(Runtime.Runtime.Not))!,
        ["Lt"] = typeof(Runtime.Runtime).GetMethod(nameof(Runtime.Runtime.Lt))!,
        ["Le"] = typeof(Runtime.Runtime).GetMethod(nameof(Runtime.Runtime.Le))!,
        ["Gt"] = typeof(Runtime.Runtime).GetMethod(nameof(Runtime.Runtime.Gt))!,
        ["Ge"] = typeof(Runtime.Runtime).GetMethod(nameof(Runtime.Runtime.Ge))!,
        ["Eq"] = typeof(Runtime.Runtime).GetMethod(nameof(Runtime.Runtime.Eq))!,
        ["Ne"] = typeof(Runtime.Runtime).GetMethod(nameof(Runtime.Runtime.Ne))!,
        ["LoadIndex"] = typeof(Runtime.Runtime).GetMethod(nameof(Runtime.Runtime.LoadIndex))!,
        ["StoreIndex"] = typeof(Runtime.Runtime).GetMethod(nameof(Runtime.Runtime.StoreIndex))!,
        ["ToBool"] = typeof(Runtime.Runtime).GetMethod(nameof(Runtime.Runtime.ToBool))!
    };

    private readonly static MethodInfo BuiltinsInvoke =
        typeof(BuiltinsRuntime).GetMethod(nameof(BuiltinsRuntime.Invoke))!;

    private (Assembly asm, Type programType) Emit(MirModule mod, string asmName = "MiniLangDyn")
    {
        var an = new AssemblyName(asmName);
        AssemblyBuilder ab = AssemblyBuilder.DefineDynamicAssembly(an, AssemblyBuilderAccess.RunAndCollect);
        ModuleBuilder mb = ab.DefineDynamicModule(asmName);
        TypeBuilder tb = mb.DefineType(
            "MiniProgram",
            TypeAttributes.Public | TypeAttributes.Sealed | TypeAttributes.Abstract);

        // Собираем список имён функций (для вызовов user→user)
        var funcNames = new HashSet<string>(mod.Functions.Select(f => f.Name));

        // Генерим методы
        var methods = new Dictionary<string, MethodBuilder>();
        foreach (MirFunction f in mod.Functions)
        {
            // сигнатура: static object? f(object?[] args)
            MethodBuilder mbuilder = tb.DefineMethod(
                f.Name,
                MethodAttributes.Public | MethodAttributes.Static,
                typeof(object),
                [typeof(object[])]);
            methods[f.Name] = mbuilder;

            ILGenerator il = mbuilder.GetILGenerator();

            // vreg → local(object)
            var locals = new Dictionary<int, LocalBuilder>();

            LocalBuilder GetLocal(VReg v)
            {
                if (!locals.TryGetValue(v.Id, out LocalBuilder? lb))
                    locals[v.Id] = lb = il.DeclareLocal(typeof(object));
                return lb;
            }

            // ==== Prologue: bind args[] to parameters (deterministic) ====
            for (int i = 0; i < f.ParamRegs.Count; i++)
            {
                il.Emit(OpCodes.Ldarg_0);           // args
                il.Emit(OpCodes.Ldc_I4, i);         // index
                il.Emit(OpCodes.Ldelem_Ref);        // args[i]
                il.Emit(OpCodes.Stloc, GetLocal(f.ParamRegs[i]));
            }

            // метки для блоков
            Dictionary<MirBlock, Label> labels = f.Blocks.ToDictionary(b => b, _ => il.DefineLabel());

            // проходим по блокам в порядке объявления
            foreach (MirBlock b in f.Blocks)
            {
                il.MarkLabel(labels[b]);

                // инструкции
                foreach (MirInstr ins in b.Instructions)
                    EmitInstr(il, ins, GetLocal, methods, funcNames);

                // терминатор
                switch (b.Terminator)
                {
                    case null:
                        break;
                    case Ret r:
                        if (r.Value is null)
                            il.Emit(OpCodes.Ldnull);
                        else
                            EmitOperand(il, r.Value, GetLocal);
                        il.Emit(OpCodes.Ret);
                        break;
                    case Br br:
                        il.Emit(OpCodes.Br, labels[br.Target]);
                        break;
                    case BrCond bc:
                        // cond → bool → brtrue
                        EmitOperand(il, bc.Cond, GetLocal);
                        il.Emit(OpCodes.Call, _rt["ToBool"]);
                        il.Emit(OpCodes.Brtrue, labels[bc.IfTrue]);
                        il.Emit(OpCodes.Br, labels[bc.IfFalse]);
                        break;
                    default:
                        throw new NotSupportedException($"Terminator {b.Terminator.GetType().Name}");
                }
            }

            // безопасность: если из последнего блока не было ret — вернуть null
            il.Emit(OpCodes.Ldnull);
            il.Emit(OpCodes.Ret);
        }

        Type programType = tb.CreateType()!;
        return (ab, programType);
    }

    public object? RunMain(MirModule mod)
    {
        (Assembly asm, Type type) = Emit(mod);
        MethodInfo main = type.GetMethod("main", BindingFlags.Public | BindingFlags.Static)
                          ?? throw new MissingMethodException("entry function 'main' not found");
        return main.Invoke(null, [Array.Empty<object?>()]);
    }

    private void EmitArgsArray(
        ILGenerator il,
        IReadOnlyList<MOperand> args,
        Func<VReg, LocalBuilder> getLocal)
    {
        il.Emit(OpCodes.Ldc_I4, args.Count);
        il.Emit(OpCodes.Newarr, typeof(object));
        for (var i = 0; i < args.Count; i++)
        {
            il.Emit(OpCodes.Dup);
            il.Emit(OpCodes.Ldc_I4, i);
            EmitOperand(il, args[i], getLocal);
            il.Emit(OpCodes.Stelem_Ref);
        }
    }

    private static void EmitConst(ILGenerator il, object? val)
    {
        switch (val)
        {
            case null: il.Emit(OpCodes.Ldnull); break;
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
                throw new NotSupportedException($"Const {val.GetType().Name}");
        }
    }

    // Emission helpers

    private void EmitInstr(
        ILGenerator il,
        MirInstr ins,
        Func<VReg, LocalBuilder> getLocal,
        Dictionary<string, MethodBuilder> methods,
        HashSet<string> funcNames)
    {
        switch (ins)
        {
            case Move mv:
            {
                EmitOperand(il, mv.Src, getLocal);
                il.Emit(OpCodes.Stloc, getLocal(mv.Dst));
                break;
            }
            case Bin bi:
            {
                EmitOperand(il, bi.L, getLocal);
                EmitOperand(il, bi.R, getLocal);
                il.Emit(OpCodes.Call, MapBin(bi.Op));
                il.Emit(OpCodes.Stloc, getLocal(bi.Dst));
                break;
            }
            case Un un:
            {
                EmitOperand(il, un.X, getLocal);
                il.Emit(OpCodes.Call, MapUn(un.Op));
                il.Emit(OpCodes.Stloc, getLocal(un.Dst));
                break;
            }
            case LoadIndex li:
            {
                EmitOperand(il, li.Arr, getLocal);
                EmitOperand(il, li.Index, getLocal);
                il.Emit(OpCodes.Call, _rt["LoadIndex"]);
                il.Emit(OpCodes.Stloc, getLocal(li.Dst));
                break;
            }
            case StoreIndex si:
            {
                EmitOperand(il, si.Arr, getLocal);
                EmitOperand(il, si.Index, getLocal);
                EmitOperand(il, si.Value, getLocal);
                il.Emit(OpCodes.Call, _rt["StoreIndex"]);
                break;
            }
            case Call cl:
            {
                // собираем object?[] args и временно сохраняем в локальную переменную
                EmitArgsArray(il, cl.Args, getLocal);
                LocalBuilder tmpArgs = il.DeclareLocal(typeof(object[]));
                il.Emit(OpCodes.Stloc, tmpArgs);

                if (funcNames.Contains(cl.Callee))
                {
                    // user function: call f(args)
                    MethodBuilder mi = methods[cl.Callee];
                    il.Emit(OpCodes.Ldloc, tmpArgs);
                    il.Emit(OpCodes.Call, mi);
                }
                else
                {
                    // builtin: BuiltinsRuntime.Invoke(name, args)
                    il.Emit(OpCodes.Ldstr, cl.Callee); // 1st arg
                    il.Emit(OpCodes.Ldloc, tmpArgs); // 2nd arg
                    il.Emit(OpCodes.Call, BuiltinsInvoke);
                }

                if (cl.Dst is not null)
                    il.Emit(OpCodes.Stloc, getLocal(cl.Dst));
                else
                    il.Emit(OpCodes.Pop);
                break;
            }
            default:
                throw new NotSupportedException($"Instr {ins.GetType().Name}");
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

    private MethodInfo MapBin(MBinOp op) => op switch
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

    private MethodInfo MapUn(MUnOp op) => op switch
    {
        MUnOp.Neg => _rt["Neg"],
        MUnOp.Not => _rt["Not"],
        MUnOp.Plus => _rt["Plus"],
        _ => throw new NotSupportedException(op.ToString())
    };
}
