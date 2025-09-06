using System.Reflection;
using System.Reflection.Emit;

using Compiler.Backend.CLR.Runtime;
using Compiler.Frontend.Translation.MIR.Common;
using Compiler.Frontend.Translation.MIR.Instructions;
using Compiler.Frontend.Translation.MIR.Instructions.Abstractions;
using Compiler.Frontend.Translation.MIR.Operands;

namespace Compiler.Backend.CLR;

public sealed partial class CilBackend
{
    private readonly Dictionary<string, MethodInfo> _rt = new Dictionary<string, MethodInfo>
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

    private static readonly MethodInfo BuiltinsInvoke =
        typeof(BuiltinsRuntime).GetMethod(nameof(BuiltinsRuntime.Invoke))!;

    public object? RunMain(
        MirModule mod)
    {
        (Assembly asm, Type type) = Emit(mod);
        MethodInfo main = type.GetMethod(
                name: "main",
                bindingAttr: BindingFlags.Public | BindingFlags.Static)
            ?? throw new MissingMethodException("entry function 'main' not found");

        return main.Invoke(
            obj: null,
            parameters: [Array.Empty<object?>()]);
    }

    private (Assembly asm, Type programType) Emit(
        MirModule mod,
        string asmName = "MiniLangDyn")
    {
        var an = new AssemblyName(asmName);
        AssemblyBuilder ab = AssemblyBuilder.DefineDynamicAssembly(
            name: an,
            access: AssemblyBuilderAccess.RunAndCollect);

        ModuleBuilder mb = ab.DefineDynamicModule(asmName);
        TypeBuilder tb = mb.DefineType(
            name: "MiniProgram",
            attr: TypeAttributes.Public | TypeAttributes.Sealed | TypeAttributes.Abstract);

        // Collect function names (for user→user calls)
        var funcNames = new HashSet<string>(mod.Functions.Select(f => f.Name));

        // Emit methods
        var methods = new Dictionary<string, MethodBuilder>();

        foreach (MirFunction f in mod.Functions)
        {
            // Signature: static object? f(object?[] args)
            MethodBuilder mbuilder = tb.DefineMethod(
                name: f.Name,
                attributes: MethodAttributes.Public | MethodAttributes.Static,
                returnType: typeof(object),
                parameterTypes: [typeof(object[])]);

            methods[f.Name] = mbuilder;

            ILGenerator il = mbuilder.GetILGenerator();

            // vreg → local(object)
            var locals = new Dictionary<int, LocalBuilder>();

            LocalBuilder GetLocal(
                VReg v)
            {
                if (!locals.TryGetValue(
                        key: v.Id,
                        value: out LocalBuilder? lb))
                {
                    Type t = GetClrTypeForVReg(
                        func: f,
                        v: v);

                    locals[v.Id] = lb = il.DeclareLocal(t);
                }

                return lb;
            }

            // ==== Prologue: bind args[] to parameters (deterministic) ====
            for (int i = 0; i < f.ParamRegs.Count; i++)
            {
                il.Emit(OpCodes.Ldarg_0); // args
                il.Emit(
                    opcode: OpCodes.Ldc_I4,
                    arg: i); // index

                il.Emit(OpCodes.Ldelem_Ref); // args[i]
                il.Emit(
                    opcode: OpCodes.Stloc,
                    local: GetLocal(f.ParamRegs[i]));
            }

            // Labels for blocks
            Dictionary<MirBlock, Label> labels = f.Blocks.ToDictionary(
                keySelector: b => b,
                elementSelector: _ => il.DefineLabel());

            // Iterate blocks in declaration order
            foreach (MirBlock b in f.Blocks)
            {
                il.MarkLabel(labels[b]);

                // Instructions
                foreach (MirInstr ins in b.Instructions)
                {
                    EmitInstr(
                        il: il,
                        f: f,
                        ins: ins,
                        getLocal: GetLocal,
                        methods: methods,
                        funcNames: funcNames);
                }

                // Terminator
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
                            il.Emit(
                                opcode: OpCodes.Ldloc,
                                local: GetLocal(rv));

                            Type rt = GetClrTypeForOperand(
                                f: f,
                                v: rv);

                            if (rt == typeof(long))
                            {
                                il.Emit(
                                    opcode: OpCodes.Box,
                                    cls: typeof(long));
                            }
                            else if (rt == typeof(bool))
                            {
                                il.Emit(
                                    opcode: OpCodes.Box,
                                    cls: typeof(bool));
                            }
                            else if (rt == typeof(char))
                            {
                                il.Emit(
                                    opcode: OpCodes.Box,
                                    cls: typeof(char));
                            }
                        }
                        else if (r.Value is Const rc)
                        {
                            EmitConst(
                                il: il,
                                val: rc.Value); // already boxed
                        }
                        else
                        {
                            // generic operand (shouldn't happen often)
                            EmitOperand(
                                il: il,
                                op: r.Value,
                                getLocal: GetLocal);
                        }

                        il.Emit(OpCodes.Ret);

                        break;
                    case Br br:
                        il.Emit(
                            opcode: OpCodes.Br,
                            label: labels[br.Target]);

                        break;
                    case BrCond bc:
                        if (bc.Cond is VReg cv && GetClrTypeForOperand(
                                f: f,
                                v: cv) == typeof(bool))
                        {
                            il.Emit(
                                opcode: OpCodes.Ldloc,
                                local: GetLocal(cv));
                        }
                        else
                        {
                            EmitLoadAsObject(
                                il: il,
                                f: f,
                                op: bc.Cond,
                                getLocal: GetLocal);

                            il.Emit(
                                opcode: OpCodes.Call,
                                meth: _rt["ToBool"]);
                        }

                        il.Emit(
                            opcode: OpCodes.Brtrue,
                            label: labels[bc.IfTrue]);

                        il.Emit(
                            opcode: OpCodes.Br,
                            label: labels[bc.IfFalse]);

                        break;
                    default:
                        throw new NotSupportedException($"Terminator {b.Terminator.GetType().Name}");
                }
            }

            // Safety: if no ret was emitted from the last block — return null
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
                EmitMove(
                    il: il,
                    f: f,
                    mv: mv,
                    getLocal: getLocal);

                break;
            case Bin bi:
                EmitBin(
                    il: il,
                    f: f,
                    bi: bi,
                    getLocal: getLocal);

                break;
            case Un un:
                EmitUn(
                    il: il,
                    f: f,
                    un: un,
                    getLocal: getLocal);

                break;
            case LoadIndex li:
                EmitLoadIndex(
                    il: il,
                    f: f,
                    li: li,
                    getLocal: getLocal);

                break;
            case StoreIndex si:
                EmitStoreIndex(
                    il: il,
                    f: f,
                    si: si,
                    getLocal: getLocal);

                break;
            case Call cl:
                EmitCall(
                    il: il,
                    f: f,
                    cl: cl,
                    getLocal: getLocal,
                    methods: methods,
                    funcNames: funcNames);

                break;
            default:
                throw new NotSupportedException($"Instr {ins.GetType().Name}");
        }
    }
}
