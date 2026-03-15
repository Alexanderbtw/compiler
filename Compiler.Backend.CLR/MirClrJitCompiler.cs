using System.Reflection;
using System.Reflection.Emit;

using Compiler.Core.Builtins;
using Compiler.Frontend.Translation.MIR.Common;
using Compiler.Frontend.Translation.MIR.Instructions;
using Compiler.Frontend.Translation.MIR.Instructions.Abstractions;
using Compiler.Frontend.Translation.MIR.Operands;
using Compiler.Frontend.Translation.MIR.Operands.Abstractions;
using Compiler.Runtime.VM;
using Compiler.Runtime.VM.Execution;

namespace Compiler.Backend.CLR;

/// <summary>
///     Compiles MIR into CLR delegates while preserving VM value semantics and runtime services.
/// </summary>
public sealed class MirClrJitCompiler
{
    private static readonly FieldInfo FiNull = typeof(VmValue).GetField(nameof(VmValue.Null))!;
    private static readonly MethodInfo MiAllocateString = typeof(IVmExecutionRuntime).GetMethod(nameof(IVmExecutionRuntime.AllocateString))!;
    private static readonly MethodInfo MiBuiltins = typeof(VmBuiltins).GetMethod(
        nameof(VmBuiltins.Invoke),
        [typeof(string), typeof(IVmExecutionRuntime), typeof(VmValue[])])!;
    private static readonly MethodInfo MiContextEnter = typeof(VmClrExecutionContext).GetMethod(nameof(VmClrExecutionContext.EnterFrame))!;
    private static readonly MethodInfo MiContextExit = typeof(VmClrExecutionContext).GetMethod(nameof(VmClrExecutionContext.ExitFrame))!;
    private static readonly MethodInfo MiContextInvoke = typeof(VmClrExecutionContext).GetMethod(nameof(VmClrExecutionContext.InvokeFunction))!;
    private static readonly MethodInfo MiFromBool = typeof(VmValue).GetMethod(nameof(VmValue.FromBool))!;
    private static readonly MethodInfo MiFromChar = typeof(VmValue).GetMethod(nameof(VmValue.FromChar))!;
    private static readonly MethodInfo MiFromLong = typeof(VmValue).GetMethod(nameof(VmValue.FromLong))!;
    private static readonly MethodInfo MiGet = typeof(VmClrJitHelpers).GetMethod(nameof(VmClrJitHelpers.Get))!;
    private static readonly MethodInfo MiI64 = typeof(VmClrJitHelpers).GetMethod(nameof(VmClrJitHelpers.I64))!;
    private static readonly MethodInfo MiLoadIndex = typeof(VmClrJitHelpers).GetMethod(nameof(VmClrJitHelpers.LoadIndex))!;
    private static readonly MethodInfo MiRuntime = typeof(VmClrExecutionContext).GetProperty(nameof(VmClrExecutionContext.Runtime))!.GetMethod!;
    private static readonly MethodInfo MiSet = typeof(VmClrJitHelpers).GetMethod(nameof(VmClrJitHelpers.Set))!;
    private static readonly MethodInfo MiStoreIndex = typeof(VmClrJitHelpers).GetMethod(nameof(VmClrJitHelpers.StoreIndex))!;
    private static readonly MethodInfo MiToBool = typeof(VmValueOps).GetMethod(nameof(VmValueOps.ToBool))!;
    private static readonly MethodInfo MiValueEquals = typeof(VmValueOps).GetMethod(nameof(VmValueOps.AreEqual))!;

    /// <summary>
    ///     Compiles a MIR module into CLR delegates.
    /// </summary>
    /// <param name="mir">Source MIR module.</param>
    /// <returns>Compiled CLR program.</returns>
    public VmClrCompiledProgram Compile(
        MirModule mir)
    {
        ArgumentNullException.ThrowIfNull(mir);

        var map = new Dictionary<string, VmClrJitFunc>(
            capacity: mir.Functions.Count,
            comparer: StringComparer.Ordinal);

        foreach (MirFunction function in mir.Functions)
        {
            map[function.Name] = CompileFunction(function);
        }

        return new VmClrCompiledProgram(map);
    }

    private static VmClrJitFunc CompileFunction(
        MirFunction function)
    {
        int maxRegisterId = function.ParamRegs.Count == 0
            ? -1
            : function.ParamRegs.Max(register => register.Id);

        foreach (MirBlock block in function.Blocks)
        {
            foreach (MirInstr instruction in block.Instructions)
            {
                maxRegisterId = Math.Max(
                    val1: maxRegisterId,
                    val2: GetMaxRegisterId(instruction));
            }
        }

        var dynamicMethod = new DynamicMethod(
            name: $"mirclrjit_{function.Name}",
            returnType: typeof(VmValue),
            parameterTypes: [typeof(VmClrExecutionContext), typeof(VmValue[])],
            m: typeof(MirClrJitCompiler).Module,
            skipVisibility: true);

        ILGenerator il = dynamicMethod.GetILGenerator();
        LocalBuilder locals = il.DeclareLocal(typeof(VmValue[]));
        LocalBuilder constants = il.DeclareLocal(typeof(VmValue[]));
        LocalBuilder tmp = il.DeclareLocal(typeof(VmValue));

        il.Emit(
            opcode: OpCodes.Ldc_I4,
            arg: Math.Max(
                val1: 1,
                val2: maxRegisterId + 1));
        il.Emit(
            opcode: OpCodes.Newarr,
            cls: typeof(VmValue));
        il.Emit(
            opcode: OpCodes.Stloc,
            local: locals);

        il.Emit(OpCodes.Ldc_I4_0);
        il.Emit(
            opcode: OpCodes.Newarr,
            cls: typeof(VmValue));
        il.Emit(
            opcode: OpCodes.Stloc,
            local: constants);

        il.Emit(OpCodes.Ldarg_0);
        il.Emit(
            opcode: OpCodes.Ldloc,
            local: locals);
        il.Emit(
            opcode: OpCodes.Ldloc,
            local: constants);
        il.Emit(
            opcode: OpCodes.Callvirt,
            meth: MiContextEnter);

        for (var index = 0; index < function.ParamRegs.Count; index++)
        {
            il.Emit(
                opcode: OpCodes.Ldloc,
                local: locals);
            il.Emit(
                opcode: OpCodes.Ldc_I4,
                arg: function.ParamRegs[index].Id);
            il.Emit(OpCodes.Ldarg_1);
            il.Emit(
                opcode: OpCodes.Ldc_I4,
                arg: index);
            il.Emit(
                opcode: OpCodes.Call,
                meth: MiGet);
            il.Emit(
                opcode: OpCodes.Call,
                meth: MiSet);
        }

        Dictionary<MirBlock, Label> labels = function.Blocks.ToDictionary(
            keySelector: block => block,
            elementSelector: _ => il.DefineLabel());

        foreach (MirBlock block in function.Blocks)
        {
            il.MarkLabel(labels[block]);

            foreach (MirInstr instruction in block.Instructions)
            {
                EmitInstruction(
                    il: il,
                    locals: locals,
                    constants: constants,
                    tmp: tmp,
                    instruction: instruction);
            }

            switch (block.Terminator)
            {
                case null:
                    break;

                case Ret ret:
                    if (ret.Value is null)
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
                            operand: ret.Value);
                    }

                    il.Emit(OpCodes.Ldarg_0);
                    il.Emit(
                        opcode: OpCodes.Callvirt,
                        meth: MiContextExit);
                    il.Emit(OpCodes.Ret);

                    break;

                case Br branch:
                    il.Emit(
                        opcode: OpCodes.Br,
                        label: labels[branch.Target]);

                    break;

                case BrCond branchCondition:
                    EmitLoadOperand(
                        il: il,
                        locals: locals,
                        operand: branchCondition.Cond);
                    il.Emit(OpCodes.Ldarg_0);
                    il.Emit(
                        opcode: OpCodes.Callvirt,
                        meth: MiRuntime);
                    il.Emit(
                        opcode: OpCodes.Call,
                        meth: MiToBool);
                    il.Emit(
                        opcode: OpCodes.Brtrue,
                        label: labels[branchCondition.IfTrue]);
                    il.Emit(
                        opcode: OpCodes.Br,
                        label: labels[branchCondition.IfFalse]);

                    break;

                default:
                    throw new NotSupportedException(
                        block.Terminator.GetType()
                            .Name);
            }
        }

        il.Emit(
            opcode: OpCodes.Ldsfld,
            field: FiNull);
        il.Emit(OpCodes.Ldarg_0);
        il.Emit(
            opcode: OpCodes.Callvirt,
            meth: MiContextExit);
        il.Emit(OpCodes.Ret);

        return (VmClrJitFunc)dynamicMethod.CreateDelegate(typeof(VmClrJitFunc));
    }

    private static void EmitInstruction(
        ILGenerator il,
        LocalBuilder locals,
        LocalBuilder constants,
        LocalBuilder tmp,
        MirInstr instruction)
    {
        switch (instruction)
        {
            case Move move:
                EmitLoadOperand(
                    il: il,
                    locals: locals,
                    operand: move.Src);
                EmitStoreRegister(
                    il: il,
                    locals: locals,
                    tmp: tmp,
                    registerId: move.Dst.Id);

                break;

            case Bin binary:
                EmitLoadOperand(
                    il: il,
                    locals: locals,
                    operand: binary.L);
                EmitLoadOperand(
                    il: il,
                    locals: locals,
                    operand: binary.R);

                if (binary.Op is MBinOp.Add or MBinOp.Sub or MBinOp.Mul or MBinOp.Div or MBinOp.Mod)
                {
                    LocalBuilder right = il.DeclareLocal(typeof(VmValue));
                    LocalBuilder left = il.DeclareLocal(typeof(VmValue));
                    il.Emit(
                        opcode: OpCodes.Stloc,
                        local: right);
                    il.Emit(
                        opcode: OpCodes.Stloc,
                        local: left);
                    il.Emit(
                        opcode: OpCodes.Ldloc,
                        local: left);
                    il.Emit(
                        opcode: OpCodes.Call,
                        meth: MiI64);
                    il.Emit(
                        opcode: OpCodes.Ldloc,
                        local: right);
                    il.Emit(
                        opcode: OpCodes.Call,
                        meth: MiI64);
                    il.Emit(
                        binary.Op switch
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
                else if (binary.Op is MBinOp.Eq or MBinOp.Ne)
                {
                    LocalBuilder right = il.DeclareLocal(typeof(VmValue));
                    LocalBuilder left = il.DeclareLocal(typeof(VmValue));
                    il.Emit(
                        opcode: OpCodes.Stloc,
                        local: right);
                    il.Emit(
                        opcode: OpCodes.Stloc,
                        local: left);
                    il.Emit(
                        opcode: OpCodes.Ldloc,
                        local: left);
                    il.Emit(
                        opcode: OpCodes.Ldloc,
                        local: right);
                    il.Emit(OpCodes.Ldarg_0);
                    il.Emit(
                        opcode: OpCodes.Callvirt,
                        meth: MiRuntime);
                    il.Emit(
                        opcode: OpCodes.Call,
                        meth: MiValueEquals);

                    if (binary.Op == MBinOp.Ne)
                    {
                        il.Emit(OpCodes.Ldc_I4_0);
                        il.Emit(OpCodes.Ceq);
                    }

                    il.Emit(
                        opcode: OpCodes.Call,
                        meth: MiFromBool);
                }
                else
                {
                    LocalBuilder right = il.DeclareLocal(typeof(VmValue));
                    LocalBuilder left = il.DeclareLocal(typeof(VmValue));
                    il.Emit(
                        opcode: OpCodes.Stloc,
                        local: right);
                    il.Emit(
                        opcode: OpCodes.Stloc,
                        local: left);
                    il.Emit(
                        opcode: OpCodes.Ldloc,
                        local: left);
                    il.Emit(
                        opcode: OpCodes.Call,
                        meth: MiI64);
                    il.Emit(
                        opcode: OpCodes.Ldloc,
                        local: right);
                    il.Emit(
                        opcode: OpCodes.Call,
                        meth: MiI64);

                    switch (binary.Op)
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

                EmitStoreRegister(
                    il: il,
                    locals: locals,
                    tmp: tmp,
                    registerId: binary.Dst.Id);

                break;

            case Un unary:
                EmitLoadOperand(
                    il: il,
                    locals: locals,
                    operand: unary.X);

                if (unary.Op == MUnOp.Neg)
                {
                    LocalBuilder operand = il.DeclareLocal(typeof(VmValue));
                    il.Emit(
                        opcode: OpCodes.Stloc,
                        local: operand);
                    il.Emit(
                        opcode: OpCodes.Ldloc,
                        local: operand);
                    il.Emit(
                        opcode: OpCodes.Call,
                        meth: MiI64);
                    il.Emit(OpCodes.Neg);
                    il.Emit(
                        opcode: OpCodes.Call,
                        meth: MiFromLong);
                }
                else if (unary.Op == MUnOp.Not)
                {
                    il.Emit(OpCodes.Ldarg_0);
                    il.Emit(
                        opcode: OpCodes.Callvirt,
                        meth: MiRuntime);
                    il.Emit(
                        opcode: OpCodes.Call,
                        meth: MiToBool);
                    il.Emit(OpCodes.Ldc_I4_0);
                    il.Emit(OpCodes.Ceq);
                    il.Emit(
                        opcode: OpCodes.Call,
                        meth: MiFromBool);
                }
                else
                {
                    LocalBuilder operand = il.DeclareLocal(typeof(VmValue));
                    il.Emit(
                        opcode: OpCodes.Stloc,
                        local: operand);
                    il.Emit(
                        opcode: OpCodes.Ldloc,
                        local: operand);
                    il.Emit(
                        opcode: OpCodes.Call,
                        meth: MiI64);
                    il.Emit(
                        opcode: OpCodes.Call,
                        meth: MiFromLong);
                }

                EmitStoreRegister(
                    il: il,
                    locals: locals,
                    tmp: tmp,
                    registerId: unary.Dst.Id);

                break;

            case LoadIndex loadIndex:
                EmitLoadOperand(
                    il: il,
                    locals: locals,
                    operand: loadIndex.Arr);
                EmitLoadOperand(
                    il: il,
                    locals: locals,
                    operand: loadIndex.Index);
                il.Emit(OpCodes.Ldarg_0);
                il.Emit(
                    opcode: OpCodes.Callvirt,
                    meth: MiRuntime);
                il.Emit(
                    opcode: OpCodes.Call,
                    meth: MiLoadIndex);
                EmitStoreRegister(
                    il: il,
                    locals: locals,
                    tmp: tmp,
                    registerId: loadIndex.Dst.Id);

                break;

            case StoreIndex storeIndex:
                EmitLoadOperand(
                    il: il,
                    locals: locals,
                    operand: storeIndex.Arr);
                EmitLoadOperand(
                    il: il,
                    locals: locals,
                    operand: storeIndex.Index);
                EmitLoadOperand(
                    il: il,
                    locals: locals,
                    operand: storeIndex.Value);
                il.Emit(OpCodes.Ldarg_0);
                il.Emit(
                    opcode: OpCodes.Callvirt,
                    meth: MiRuntime);
                il.Emit(
                    opcode: OpCodes.Call,
                    meth: MiStoreIndex);

                break;

            case Call call:
                LocalBuilder arguments = il.DeclareLocal(typeof(VmValue[]));
                il.Emit(
                    opcode: OpCodes.Ldc_I4,
                    arg: call.Args.Count);
                il.Emit(
                    opcode: OpCodes.Newarr,
                    cls: typeof(VmValue));
                il.Emit(
                    opcode: OpCodes.Stloc,
                    local: arguments);

                for (var index = 0; index < call.Args.Count; index++)
                {
                    il.Emit(
                        opcode: OpCodes.Ldloc,
                        local: arguments);
                    il.Emit(
                        opcode: OpCodes.Ldc_I4,
                        arg: index);
                    EmitLoadOperand(
                        il: il,
                        locals: locals,
                        operand: call.Args[index]);
                    il.Emit(
                        opcode: OpCodes.Stelem,
                        cls: typeof(VmValue));
                }

                if (BuiltinCatalog.Exists(call.Callee))
                {
                    il.Emit(
                        opcode: OpCodes.Ldstr,
                        str: call.Callee);
                    il.Emit(OpCodes.Ldarg_0);
                    il.Emit(
                        opcode: OpCodes.Callvirt,
                        meth: MiRuntime);
                    il.Emit(
                        opcode: OpCodes.Ldloc,
                        local: arguments);
                    il.Emit(
                        opcode: OpCodes.Call,
                        meth: MiBuiltins);
                }
                else
                {
                    il.Emit(OpCodes.Ldarg_0);
                    il.Emit(
                        opcode: OpCodes.Ldstr,
                        str: call.Callee);
                    il.Emit(
                        opcode: OpCodes.Ldloc,
                        local: arguments);
                    il.Emit(
                        opcode: OpCodes.Callvirt,
                        meth: MiContextInvoke);
                }

                if (call.Dst is { } destinationRegister)
                {
                    EmitStoreRegister(
                        il: il,
                        locals: locals,
                        tmp: tmp,
                        registerId: destinationRegister.Id);
                }
                else
                {
                    il.Emit(OpCodes.Pop);
                }

                break;

            default:
                throw new NotSupportedException(
                    instruction.GetType()
                        .Name);
        }
    }

    private static void EmitLoadOperand(
        ILGenerator il,
        LocalBuilder locals,
        MOperand operand)
    {
        switch (operand)
        {
            case Const constant:
                if (constant.Value is null)
                {
                    il.Emit(
                        opcode: OpCodes.Ldsfld,
                        field: FiNull);
                }
                else if (constant.Value is long longValue)
                {
                    il.Emit(
                        opcode: OpCodes.Ldc_I8,
                        arg: longValue);
                    il.Emit(
                        opcode: OpCodes.Call,
                        meth: MiFromLong);
                }
                else if (constant.Value is bool boolValue)
                {
                    il.Emit(boolValue
                        ? OpCodes.Ldc_I4_1
                        : OpCodes.Ldc_I4_0);
                    il.Emit(
                        opcode: OpCodes.Call,
                        meth: MiFromBool);
                }
                else if (constant.Value is char charValue)
                {
                    il.Emit(
                        opcode: OpCodes.Ldc_I4,
                        arg: charValue);
                    il.Emit(
                        opcode: OpCodes.Call,
                        meth: MiFromChar);
                }
                else if (constant.Value is string stringValue)
                {
                    il.Emit(OpCodes.Ldarg_0);
                    il.Emit(
                        opcode: OpCodes.Callvirt,
                        meth: MiRuntime);
                    il.Emit(
                        opcode: OpCodes.Ldstr,
                        str: stringValue);
                    il.Emit(
                        opcode: OpCodes.Callvirt,
                        meth: MiAllocateString);
                }
                else
                {
                    throw new NotSupportedException($"const {constant.Value.GetType().Name}");
                }

                break;

            case VReg register:
                il.Emit(
                    opcode: OpCodes.Ldloc,
                    local: locals);
                il.Emit(
                    opcode: OpCodes.Ldc_I4,
                    arg: register.Id);
                il.Emit(
                    opcode: OpCodes.Call,
                    meth: MiGet);

                break;

            default:
                throw new NotSupportedException(
                    operand.GetType()
                        .Name);
        }
    }

    private static void EmitStoreRegister(
        ILGenerator il,
        LocalBuilder locals,
        LocalBuilder tmp,
        int registerId)
    {
        il.Emit(
            opcode: OpCodes.Stloc,
            local: tmp);
        il.Emit(
            opcode: OpCodes.Ldloc,
            local: locals);
        il.Emit(
            opcode: OpCodes.Ldc_I4,
            arg: registerId);
        il.Emit(
            opcode: OpCodes.Ldloc,
            local: tmp);
        il.Emit(
            opcode: OpCodes.Call,
            meth: MiSet);
    }

    private static int GetMaxRegisterId(
        MirInstr instruction)
    {
        var maxRegisterId = -1;

        void ConsiderOperand(
            MOperand operand)
        {
            if (operand is VReg register)
            {
                maxRegisterId = Math.Max(
                    val1: maxRegisterId,
                    val2: register.Id);
            }
        }

        switch (instruction)
        {
            case Move move:
                maxRegisterId = Math.Max(
                    val1: maxRegisterId,
                    val2: move.Dst.Id);
                ConsiderOperand(move.Src);

                break;
            case Bin binary:
                maxRegisterId = Math.Max(
                    val1: maxRegisterId,
                    val2: binary.Dst.Id);
                ConsiderOperand(binary.L);
                ConsiderOperand(binary.R);

                break;
            case Un unary:
                maxRegisterId = Math.Max(
                    val1: maxRegisterId,
                    val2: unary.Dst.Id);
                ConsiderOperand(unary.X);

                break;
            case LoadIndex loadIndex:
                maxRegisterId = Math.Max(
                    val1: maxRegisterId,
                    val2: loadIndex.Dst.Id);
                ConsiderOperand(loadIndex.Arr);
                ConsiderOperand(loadIndex.Index);

                break;
            case StoreIndex storeIndex:
                ConsiderOperand(storeIndex.Arr);
                ConsiderOperand(storeIndex.Index);
                ConsiderOperand(storeIndex.Value);

                break;
            case Call call:
                if (call.Dst is not null)
                {
                    maxRegisterId = Math.Max(
                        val1: maxRegisterId,
                        val2: call.Dst.Id);
                }

                foreach (MOperand argument in call.Args)
                {
                    ConsiderOperand(argument);
                }

                break;
        }

        return maxRegisterId;
    }
}
