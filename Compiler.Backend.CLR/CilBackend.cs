using System.Reflection;
using System.Reflection.Emit;

using Compiler.Backend.CLR.Runtime;
using Compiler.Frontend.Translation.MIR;
using Compiler.Frontend.Translation.MIR.Common;
using Compiler.Frontend.Translation.MIR.Instructions;
using Compiler.Frontend.Translation.MIR.Instructions.Abstractions;
using Compiler.Frontend.Translation.MIR.Operands;

namespace Compiler.Backend.CLR;

public sealed partial class CilBackend
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

    public object? RunMain(MirModule mod)
    {
        (Assembly asm, Type type) = Emit(mod);
        MethodInfo main = type.GetMethod("main", BindingFlags.Public | BindingFlags.Static)
                          ?? throw new MissingMethodException("entry function 'main' not found");
        return main.Invoke(null, [Array.Empty<object?>()]);
    }

    private (Assembly asm, Type programType) Emit(MirModule mod, string asmName = "MiniLangDyn")
    {
        var an = new AssemblyName(asmName);
        AssemblyBuilder ab = AssemblyBuilder.DefineDynamicAssembly(an, AssemblyBuilderAccess.RunAndCollect);
        ModuleBuilder mb = ab.DefineDynamicModule(asmName);
        TypeBuilder tb = mb.DefineType(
            "MiniProgram",
            TypeAttributes.Public | TypeAttributes.Sealed | TypeAttributes.Abstract);

        // MIR simplification: const-folding + copy-prop + branch folding
        new MirSimplifier().Run(mod);

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

            // Инференция типов для функции
            new MirTypeAnnotator().Annotate(f);

            // vreg → local(object)
            var locals = new Dictionary<int, LocalBuilder>();

            LocalBuilder GetLocal(VReg v)
            {
                if (!locals.TryGetValue(v.Id, out LocalBuilder? lb))
                {
                    Type t = GetClrTypeForVReg(f, v);
                    locals[v.Id] = lb = il.DeclareLocal(t);
                }
                return lb;
            }

            // ==== Prologue: bind args[] to parameters (deterministic) ====
            for (var i = 0; i < f.ParamRegs.Count; i++)
            {
                il.Emit(OpCodes.Ldarg_0); // args
                il.Emit(OpCodes.Ldc_I4, i); // index
                il.Emit(OpCodes.Ldelem_Ref); // args[i]
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
                    EmitInstr(il, f, ins, GetLocal, methods, funcNames);

                // терминатор
                switch (b.Terminator)
                {
                    case null:
                        break;
                    case Ret r:
                        if (r.Value is null)
                        {
                            il.Emit(OpCodes.Ldnull);
                        }
                        else if (r.Value is VReg rv)
                        {
                            il.Emit(OpCodes.Ldloc, GetLocal(rv));
                            Type rt = GetClrTypeForOperand(f, rv);
                            if (rt == typeof(long)) il.Emit(OpCodes.Box, typeof(long));
                            else if (rt == typeof(bool)) il.Emit(OpCodes.Box, typeof(bool));
                            else if (rt == typeof(char)) il.Emit(OpCodes.Box, typeof(char));
                        }
                        else if (r.Value is Const rc)
                        {
                            EmitConst(il, rc.Value); // already boxed
                        }
                        else
                        {
                            // generic operand (shouldn't happen often)
                            EmitOperand(il, r.Value, GetLocal);
                        }
                        il.Emit(OpCodes.Ret);
                        break;
                    case Br br:
                        il.Emit(OpCodes.Br, labels[br.Target]);
                        break;
                    case BrCond bc:
                        if (bc.Cond is VReg cv && GetClrTypeForOperand(f, cv) == typeof(bool))
                        {
                            il.Emit(OpCodes.Ldloc, GetLocal(cv));
                        }
                        else
                        {
                            EmitLoadAsObject(il, f, bc.Cond, GetLocal);
                            il.Emit(OpCodes.Call, _rt["ToBool"]);
                        }
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

    // Emission helpers

    private void EmitInstr(
        ILGenerator il,
        MirFunction f,
        MirInstr ins,
        Func<VReg, LocalBuilder> getLocal,
        Dictionary<string, MethodBuilder> methods,
        HashSet<string> funcNames)
    {
        switch (ins)
        {
            case Move mv:
                EmitMove(il, f, mv, getLocal);
                break;
            case Bin bi:
                EmitBin(il, f, bi, getLocal);
                break;
            case Un un:
                EmitUn(il, f, un, getLocal);
                break;
            case LoadIndex li:
                EmitLoadIndex(il, f, li, getLocal);
                break;
            case StoreIndex si:
                EmitStoreIndex(il, f, si, getLocal);
                break;
            case Call cl:
                EmitCall(il, f, cl, getLocal, methods, funcNames);
                break;
            default:
                throw new NotSupportedException($"Instr {ins.GetType().Name}");
        }
    }
}
