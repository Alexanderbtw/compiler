using System.Runtime.InteropServices;
using System.Text;

using Compiler.Frontend.Translation.HIR.Metadata;
using Compiler.Frontend.Translation.MIR.Common;
using Compiler.Frontend.Translation.MIR.Instructions;
using Compiler.Frontend.Translation.MIR.Instructions.Abstractions;
using Compiler.Frontend.Translation.MIR.Operands;
using Compiler.Frontend.Translation.MIR.Operands.Abstractions;

using LLVMSharp.Interop;

using InteropLLVM = LLVMSharp.Interop.LLVM;

namespace Compiler.Backend.JIT.LLVM;

/// <summary>
///     MIR → LLVM IR emitter.
///     All values are i64.
/// </summary>
public sealed class LlvmEmitter
{
    private static readonly LLVMContextRef Context = LLVMContextRef.Create();
    private static int StringCounter;
    public LLVMModuleRef EmitModule(
        MirModule mirModule)
    {
        // Keep one process-wide context; avoid per-call churn.
        var module = LLVMModuleRef.CreateWithName("minilang_llvm");

        LLVMTypeRef int64Type = LLVMTypeRef.Int64;

        // Predeclare all functions (uniform i64 ABI for now)
        var functionMap = new Dictionary<MirFunction, LLVMValueRef>(mirModule.Functions.Count);
        var functionTypeMap = new Dictionary<MirFunction, LLVMTypeRef>(mirModule.Functions.Count);

        foreach (MirFunction mirFunction in mirModule.Functions)
        {
            var parameterTypes = new LLVMTypeRef[mirFunction.ParamRegs.Count];

            for (int i = 0; i < parameterTypes.Length; i++)
            {
                parameterTypes[i] = int64Type;
            }

            var functionType = LLVMTypeRef.CreateFunction(
                ReturnType: int64Type,
                ParamTypes: parameterTypes.AsSpan(),
                IsVarArg: false);

            LLVMValueRef llvmFunction = module.AddFunction(
                Name: mirFunction.Name,
                FunctionTy: functionType);

            functionMap[mirFunction] = llvmFunction;
            functionTypeMap[mirFunction] = functionType;
        }

        // Create basic blocks per function
        var blockMaps =
            new Dictionary<MirFunction, Dictionary<MirBlock, LLVMBasicBlockRef>>(mirModule.Functions.Count);

        foreach (MirFunction mirFunction in mirModule.Functions)
        {
            var blockMap = new Dictionary<MirBlock, LLVMBasicBlockRef>(mirFunction.Blocks.Count);
            LLVMValueRef llvmFunction = functionMap[mirFunction];

            foreach (MirBlock mirBlock in mirFunction.Blocks)
            {
                blockMap[mirBlock] = llvmFunction.AppendBasicBlock(mirBlock.Name);
            }

            blockMaps[mirFunction] = blockMap;
        }

        var builder = LLVMBuilderRef.Create(Context);

        // Emit function bodies
        foreach (MirFunction mirFunction in mirModule.Functions)
        {
            LLVMValueRef llvmFunction = functionMap[mirFunction];
            Dictionary<MirBlock, LLVMBasicBlockRef> blockMap = blockMaps[mirFunction];
            var boolRegs = new HashSet<int>();

            // Compute max vreg to allocate storage slots
            int maxVirtualRegisterId = ComputeMaxVirtualRegisterId(mirFunction);
            var registerSlots = new Dictionary<int, LLVMValueRef>(
                Math.Max(
                    val1: 0,
                    val2: maxVirtualRegisterId + 1));

            // Create slots (alloca) in entry block
            LLVMBasicBlockRef entryBlock = blockMap[mirFunction.Blocks[0]];
            builder.PositionAtEnd(entryBlock);

            // Begin a new handle frame for object handles allocated in this function
            // long ml_handle_push_frame()
            LLVMValueRef pushFrameFn = EnsureBuiltin(
                module: module,
                name: "ml_handle_push_frame",
                paramTypes: ReadOnlySpan<LLVMTypeRef>.Empty,
                int64Type: int64Type);

            _ = builder.BuildCall2(
                Ty: LLVMTypeRef.CreateFunction(
                    ReturnType: int64Type,
                    ParamTypes: ReadOnlySpan<LLVMTypeRef>.Empty,
                    IsVarArg: false),
                Fn: pushFrameFn,
                Args: ReadOnlySpan<LLVMValueRef>.Empty,
                Name: "push_frame");

            for (int id = 0; id <= maxVirtualRegisterId; id++)
            {
                registerSlots[id] = builder.BuildAlloca(
                    Ty: int64Type,
                    Name: $"v{id}");
            }

            // Store function parameters into their corresponding vreg slots
            for (int parameterIndex = 0; parameterIndex < mirFunction.ParamRegs.Count; parameterIndex++)
            {
                LLVMValueRef parameterValue = llvmFunction.GetParam((uint)parameterIndex);
                VReg parameterRegister = mirFunction.ParamRegs[parameterIndex];

                builder.BuildStore(
                    Val: parameterValue,
                    Ptr: registerSlots[parameterRegister.Id]);
            }

            // Emit instructions and terminators
            foreach (MirBlock mirBlock in mirFunction.Blocks)
            {
                builder.PositionAtEnd(blockMap[mirBlock]);

                foreach (MirInstr instruction in mirBlock.Instructions)
                {
                    switch (instruction)
                    {
                        case Move move:
                            EmitMove(
                                module: module,
                                builder: builder,
                                int64Type: int64Type,
                                registerSlots: registerSlots,
                                boolRegs: boolRegs,
                                move: move);

                            break;

                        case Un unary:
                            EmitUnary(
                                module: module,
                                builder: builder,
                                int64Type: int64Type,
                                registerSlots: registerSlots,
                                boolRegs: boolRegs,
                                unary: unary);

                            break;

                        case Bin binary:
                            EmitBinary(
                                module: module,
                                builder: builder,
                                int64Type: int64Type,
                                registerSlots: registerSlots,
                                boolRegs: boolRegs,
                                binary: binary);

                            break;

                        case Call call:
                            EmitCall(
                                module: module,
                                builder: builder,
                                int64Type: int64Type,
                                registerSlots: registerSlots,
                                functionMap: functionMap,
                                functionTypeMap: functionTypeMap,
                                boolRegs: boolRegs,
                                call: call);

                            break;

                        case LoadIndex load:
                            EmitLoadIndex(
                                module: module,
                                builder: builder,
                                int64Type: int64Type,
                                registerSlots: registerSlots,
                                load: load);

                            break;

                        case StoreIndex store:
                            EmitStoreIndex(
                                module: module,
                                builder: builder,
                                int64Type: int64Type,
                                registerSlots: registerSlots,
                                store: store);

                            break;

                        default:
                            throw new NotSupportedException($"LLVM JIT: instruction {instruction.GetType().Name} not supported");
                    }
                }

                switch (mirBlock.Terminator)
                {
                    case null:
                        // Dead/placeholder blocks still need a terminator for the verifier
                        builder.BuildUnreachable();

                        break;
                    case Ret ret:
                        {
                            LLVMValueRef returnValue = ret.Value is null
                                ? LLVMValueRef.CreateConstInt(
                                    IntTy: int64Type,
                                    N: 0,
                                    SignExtend: false)
                                : EmitLoadOperandI64(
                                    module: module,
                                    builder: builder,
                                    int64Type: int64Type,
                                    registerSlots: registerSlots,
                                    operand: ret.Value);

                            // Transfer object handle (if any) to caller's frame
                            LLVMValueRef transferFn = EnsureBuiltin(
                                module: module,
                                name: "ml_handle_transfer",
                                paramTypes: new[] { int64Type },
                                int64Type: int64Type);

                            Span<LLVMValueRef> argvTx = [returnValue];
                            LLVMValueRef retTx = builder.BuildCall2(
                                Ty: LLVMTypeRef.CreateFunction(
                                    ReturnType: int64Type,
                                    ParamTypes: new[] { int64Type },
                                    IsVarArg: false),
                                Fn: transferFn,
                                Args: argvTx,
                                Name: "transfer");

                            // Pop the handle frame before returning
                            LLVMValueRef popFrameFn = EnsureBuiltin(
                                module: module,
                                name: "ml_handle_pop_frame",
                                paramTypes: ReadOnlySpan<LLVMTypeRef>.Empty,
                                int64Type: int64Type);

                            _ = builder.BuildCall2(
                                Ty: LLVMTypeRef.CreateFunction(
                                    ReturnType: int64Type,
                                    ParamTypes: ReadOnlySpan<LLVMTypeRef>.Empty,
                                    IsVarArg: false),
                                Fn: popFrameFn,
                                Args: ReadOnlySpan<LLVMValueRef>.Empty,
                                Name: "pop_frame");

                            builder.BuildRet(retTx);

                            break;
                        }
                    case Br br:
                        builder.BuildBr(blockMap[br.Target]);

                        break;
                    case BrCond brCond:
                        {
                            LLVMValueRef condValue = EmitLoadOperandI64(
                                module: module,
                                builder: builder,
                                int64Type: int64Type,
                                registerSlots: registerSlots,
                                operand: brCond.Cond);

                            var zero = LLVMValueRef.CreateConstInt(
                                IntTy: int64Type,
                                N: 0,
                                SignExtend: false);

                            LLVMValueRef predicate = builder.BuildICmp(
                                Op: LLVMIntPredicate.LLVMIntNE,
                                LHS: condValue,
                                RHS: zero,
                                Name: "cond");

                            builder.BuildCondBr(
                                If: predicate,
                                Then: blockMap[brCond.IfTrue],
                                Else: blockMap[brCond.IfFalse]);

                            break;
                        }
                    default:
                        throw new NotSupportedException($"LLVM JIT: terminator {mirBlock.Terminator.GetType().Name} not supported");
                }
            }
        }

        // Builder no longer needed after IR emitted
        builder.Dispose();

        return module;
    }

    private static unsafe LLVMValueRef BuildCStringPtr(
        LLVMModuleRef module,
        LLVMBuilderRef builder,
        string s)
    {
        // Create a private unnamed_addr global [N x i8] with explicit null terminator
        // and return i8* via GEP [0, 0].
        byte[] bytes = Encoding.UTF8.GetBytes(s + "\0");
        uint len = (uint)bytes.Length;

        var arrayTy = LLVMTypeRef.CreateArray(
            ElementType: LLVMTypeRef.Int8,
            ElementCount: len);

        LLVMValueRef dataConst;

        fixed (byte* p = bytes)
        {
            // We already added a trailing \0; tell LLVM not to append another terminator.
            dataConst = InteropLLVM.ConstString(
                Str: (sbyte*)p,
                Length: len,
                DontNullTerminate: 1);
        }

        string gname = $".str_{Interlocked.Increment(ref StringCounter)}";
        LLVMValueRef global = module.AddGlobal(
            Ty: arrayTy,
            Name: gname);

        InteropLLVM.SetLinkage(
            Global: global,
            Linkage: LLVMLinkage.LLVMPrivateLinkage);

        InteropLLVM.SetUnnamedAddress(
            Global: global,
            UnnamedAddr: LLVMUnnamedAddr.LLVMGlobalUnnamedAddr);

        InteropLLVM.SetGlobalConstant(
            GlobalVar: global,
            IsConstant: 1);

        InteropLLVM.SetAlignment(
            V: global,
            Bytes: 1);

        InteropLLVM.SetInitializer(
            GlobalVar: global,
            ConstantVal: dataConst);

        var zero = LLVMValueRef.CreateConstInt(
            IntTy: LLVMTypeRef.Int64,
            N: 0,
            SignExtend: false);

        Span<LLVMValueRef> idx = [zero, zero];

        return builder.BuildInBoundsGEP2(
            Ty: arrayTy,
            Pointer: global,
            Indices: idx,
            Name: "str_gep");
    }

    private static int ComputeMaxVirtualRegisterId(
        MirFunction mirFunction)
    {
        int maxId = -1;

        foreach (MirBlock block in mirFunction.Blocks)
        foreach (MirInstr instruction in block.Instructions)
        {
            switch (instruction)
            {
                case Move move:
                    maxId = Math.Max(
                        val1: maxId,
                        val2: move.Dst.Id);

                    if (move.Src is VReg moveSrcReg)
                    {
                        maxId = Math.Max(
                            val1: maxId,
                            val2: moveSrcReg.Id);
                    }

                    break;

                case Un unary:
                    maxId = Math.Max(
                        val1: maxId,
                        val2: unary.Dst.Id);

                    if (unary.X is VReg unarySrcReg)
                    {
                        maxId = Math.Max(
                            val1: maxId,
                            val2: unarySrcReg.Id);
                    }

                    break;

                case Bin binary:
                    maxId = Math.Max(
                        val1: maxId,
                        val2: binary.Dst.Id);

                    if (binary.L is VReg l)
                    {
                        maxId = Math.Max(
                            val1: maxId,
                            val2: l.Id);
                    }

                    if (binary.R is VReg r)
                    {
                        maxId = Math.Max(
                            val1: maxId,
                            val2: r.Id);
                    }

                    break;

                case Call call:
                    if (call.Dst is not null)
                    {
                        maxId = Math.Max(
                            val1: maxId,
                            val2: call.Dst.Id);
                    }

                    foreach (MOperand a in call.Args)
                    {
                        if (a is VReg vr)
                        {
                            maxId = Math.Max(
                                val1: maxId,
                                val2: vr.Id);
                        }
                    }

                    break;
            }
        }

        return maxId;
    }

    private static void EmitBinary(
        LLVMModuleRef module,
        LLVMBuilderRef builder,
        LLVMTypeRef int64Type,
        Dictionary<int, LLVMValueRef> registerSlots,
        HashSet<int> boolRegs,
        Bin binary)
    {
        LLVMValueRef left = EmitLoadOperandI64(
            module: module,
            builder: builder,
            int64Type: int64Type,
            registerSlots: registerSlots,
            operand: binary.L);

        LLVMValueRef right = EmitLoadOperandI64(
            module: module,
            builder: builder,
            int64Type: int64Type,
            registerSlots: registerSlots,
            operand: binary.R);

        LLVMValueRef result = binary.Op switch
        {
            MBinOp.Add => builder.BuildAdd(
                LHS: left,
                RHS: right,
                Name: "add"),
            MBinOp.Sub => builder.BuildSub(
                LHS: left,
                RHS: right,
                Name: "sub"),
            MBinOp.Mul => builder.BuildMul(
                LHS: left,
                RHS: right,
                Name: "mul"),
            MBinOp.Div => builder.BuildSDiv(
                LHS: left,
                RHS: right,
                Name: "div"),
            MBinOp.Mod => builder.BuildSRem(
                LHS: left,
                RHS: right,
                Name: "rem"),
            MBinOp.Eq => builder.BuildCall2(
                Ty: LLVMTypeRef.CreateFunction(
                    ReturnType: int64Type,
                    ParamTypes: new[] { int64Type, int64Type },
                    IsVarArg: false),
                Fn: EnsureBuiltin(
                    module: module,
                    name: "ml_eq",
                    paramTypes: new[] { int64Type, int64Type },
                    int64Type: int64Type),
                Args: stackalloc LLVMValueRef[] { left, right },
                Name: "eq"),
            MBinOp.Ne => builder.BuildCall2(
                Ty: LLVMTypeRef.CreateFunction(
                    ReturnType: int64Type,
                    ParamTypes: new[] { int64Type, int64Type },
                    IsVarArg: false),
                Fn: EnsureBuiltin(
                    module: module,
                    name: "ml_ne",
                    paramTypes: new[] { int64Type, int64Type },
                    int64Type: int64Type),
                Args: stackalloc LLVMValueRef[] { left, right },
                Name: "ne"),
            MBinOp.Lt => builder.BuildZExt(
                Val: builder.BuildICmp(
                    Op: LLVMIntPredicate.LLVMIntSLT,
                    LHS: left,
                    RHS: right,
                    Name: "lt"),
                DestTy: int64Type,
                Name: "zext"),
            MBinOp.Le => builder.BuildZExt(
                Val: builder.BuildICmp(
                    Op: LLVMIntPredicate.LLVMIntSLE,
                    LHS: left,
                    RHS: right,
                    Name: "le"),
                DestTy: int64Type,
                Name: "zext"),
            MBinOp.Gt => builder.BuildZExt(
                Val: builder.BuildICmp(
                    Op: LLVMIntPredicate.LLVMIntSGT,
                    LHS: left,
                    RHS: right,
                    Name: "gt"),
                DestTy: int64Type,
                Name: "zext"),
            MBinOp.Ge => builder.BuildZExt(
                Val: builder.BuildICmp(
                    Op: LLVMIntPredicate.LLVMIntSGE,
                    LHS: left,
                    RHS: right,
                    Name: "ge"),
                DestTy: int64Type,
                Name: "zext"),
            _ => throw new NotSupportedException($"LLVM JIT: binop {binary.Op} not supported")
        };

        builder.BuildStore(
            Val: result,
            Ptr: registerSlots[binary.Dst.Id]);

        // Track boolean-typed registers
        switch (binary.Op)
        {
            case MBinOp.Eq or MBinOp.Ne or MBinOp.Lt or MBinOp.Le or MBinOp.Gt or MBinOp.Ge:
                boolRegs.Add(binary.Dst.Id);

                break;
            default:
                boolRegs.Remove(binary.Dst.Id);

                break;
        }
    }

    private static void EmitCall(
        LLVMModuleRef module,
        LLVMBuilderRef builder,
        LLVMTypeRef int64Type,
        Dictionary<int, LLVMValueRef> registerSlots,
        Dictionary<MirFunction, LLVMValueRef> functionMap,
        Dictionary<MirFunction, LLVMTypeRef> functionTypeMap,
        HashSet<int> boolRegs,
        Call call)
    {
        if (IsBuiltinName(call.Callee))
        {
            // Generic builtin glue path: i64 ml_builtin_invoke(i8* name, i64* argv, i32 argc)
            LLVMValueRef namePtrGen = BuildCStringPtr(
                module: module,
                builder: builder,
                s: call.Callee);

            int argcGen = call.Args.Count;
            var arrayTyGen = LLVMTypeRef.CreateArray(
                ElementType: int64Type,
                ElementCount: (uint)argcGen);

            LLVMValueRef argvAllocGen = builder.BuildAlloca(
                Ty: arrayTyGen,
                Name: "argv");

            var zero64Gen = LLVMValueRef.CreateConstInt(
                IntTy: LLVMTypeRef.Int64,
                N: 0,
                SignExtend: false);

            Span<LLVMValueRef> idxHeadGen = stackalloc LLVMValueRef[] { zero64Gen, zero64Gen };
            LLVMValueRef argvPtrGen = builder.BuildInBoundsGEP2(
                Ty: arrayTyGen,
                Pointer: argvAllocGen,
                Indices: idxHeadGen,
                Name: "argv_i64p");

            for (int i = 0; i < argcGen; i++)
            {
                LLVMValueRef val = EmitLoadOperandI64(
                    module: module,
                    builder: builder,
                    int64Type: int64Type,
                    registerSlots: registerSlots,
                    operand: call.Args[i]);

                // For 'print', wrap boolean vregs to Value(bool) handles for CIL parity
                if (call.Callee == "print" && call.Args[i] is VReg vr && boolRegs.Contains(vr.Id))
                {
                    val = builder.BuildCall2(
                        Ty: LLVMTypeRef.CreateFunction(
                            ReturnType: int64Type,
                            ParamTypes: new[] { int64Type },
                            IsVarArg: false),
                        Fn: EnsureBuiltin(
                            module: module,
                            name: "ml_bool_wrap",
                            paramTypes: new[] { int64Type },
                            int64Type: int64Type),
                        Args: stackalloc LLVMValueRef[] { val },
                        Name: "bool_wrap");
                }

                var idx = LLVMValueRef.CreateConstInt(
                    IntTy: LLVMTypeRef.Int64,
                    N: (ulong)i,
                    SignExtend: false);

                Span<LLVMValueRef> elemIdx = stackalloc LLVMValueRef[] { idx };
                LLVMValueRef elemPtr = builder.BuildInBoundsGEP2(
                    Ty: int64Type,
                    Pointer: argvPtrGen,
                    Indices: elemIdx,
                    Name: $"arg{i}");

                _ = builder.BuildStore(
                    Val: val,
                    Ptr: elemPtr);
            }

            LLVMValueRef glue = EnsureBuiltin(
                module: module,
                name: "ml_builtin_invoke",
                paramTypes: new[]
                {
                    LLVMTypeRef.CreatePointer(
                        ElementType: LLVMTypeRef.Int8,
                        AddressSpace: 0),
                    LLVMTypeRef.CreatePointer(
                        ElementType: int64Type,
                        AddressSpace: 0),
                    LLVMTypeRef.Int32
                },
                int64Type: int64Type);

            var argcConstGen = LLVMValueRef.CreateConstInt(
                IntTy: LLVMTypeRef.Int32,
                N: (ulong)argcGen,
                SignExtend: false);

            LLVMValueRef retGen = builder.BuildCall2(
                Ty: LLVMTypeRef.CreateFunction(
                    ReturnType: int64Type,
                    ParamTypes: new[]
                    {
                        LLVMTypeRef.CreatePointer(
                            ElementType: LLVMTypeRef.Int8,
                            AddressSpace: 0),
                        LLVMTypeRef.CreatePointer(
                            ElementType: int64Type,
                            AddressSpace: 0),
                        LLVMTypeRef.Int32
                    },
                    IsVarArg: false),
                Fn: glue,
                Args: stackalloc LLVMValueRef[] { namePtrGen, argvPtrGen, argcConstGen },
                Name: "builtin_invoke");

            if (call.Dst is not null)
            {
                builder.BuildStore(
                    Val: retGen,
                    Ptr: registerSlots[call.Dst.Id]);
            }
        }

        // Resolve callee among MIR functions
        MirFunction? calleeMir = functionMap.Keys.FirstOrDefault(f => f.Name == call.Callee);

        if (calleeMir is null)
        {
            throw new NotSupportedException($"LLVM JIT: call to unknown function '{call.Callee}'");
        }

        LLVMValueRef calleeValue = functionMap[calleeMir];
        LLVMTypeRef calleeType = functionTypeMap[calleeMir];

        var arguments = new List<LLVMValueRef>(call.Args.Count);

        foreach (MOperand arg in call.Args)
        {
            arguments.Add(
                EmitLoadOperandI64(
                    module: module,
                    builder: builder,
                    int64Type: int64Type,
                    registerSlots: registerSlots,
                    operand: arg));
        }

        LLVMValueRef callValue = builder.BuildCall2(
            Ty: calleeType,
            Fn: calleeValue,
            Args: CollectionsMarshal.AsSpan(arguments),
            Name: "call");

        if (call.Dst is not null)
        {
            builder.BuildStore(
                Val: callValue,
                Ptr: registerSlots[call.Dst.Id]);
        }
    }

    private static void EmitLoadIndex(
        LLVMModuleRef module,
        LLVMBuilderRef builder,
        LLVMTypeRef int64Type,
        Dictionary<int, LLVMValueRef> registerSlots,
        LoadIndex load)
    {
        LLVMValueRef fn = EnsureBuiltin(
            module: module,
            name: "ml_loadidx",
            paramTypes: new[] { int64Type, int64Type },
            int64Type: int64Type);

        LLVMValueRef arr = EmitLoadOperandI64(
            module: module,
            builder: builder,
            int64Type: int64Type,
            registerSlots: registerSlots,
            operand: load.Arr);

        LLVMValueRef idx = EmitLoadOperandI64(
            module: module,
            builder: builder,
            int64Type: int64Type,
            registerSlots: registerSlots,
            operand: load.Index);

        Span<LLVMValueRef> argv = [arr, idx];
        LLVMValueRef ret = builder.BuildCall2(
            Ty: LLVMTypeRef.CreateFunction(
                ReturnType: int64Type,
                ParamTypes: new[] { int64Type, int64Type },
                IsVarArg: false),
            Fn: fn,
            Args: argv,
            Name: "loadidx");

        builder.BuildStore(
            Val: ret,
            Ptr: registerSlots[load.Dst.Id]);
    }

    private static LLVMValueRef EmitLoadOperandI64(
        LLVMModuleRef module,
        LLVMBuilderRef builder,
        LLVMTypeRef int64Type,
        Dictionary<int, LLVMValueRef> registerSlots,
        MOperand operand)
    {
        switch (operand)
        {
            case Const constant:
                switch (constant.Value)
                {
                    case null:
                        return LLVMValueRef.CreateConstInt(
                            IntTy: int64Type,
                            N: 0,
                            SignExtend: false);
                    case long n:
                        return LLVMValueRef.CreateConstInt(
                            IntTy: int64Type,
                            N: unchecked((ulong)n),
                            SignExtend: true);
                    case bool b:
                        return LLVMValueRef.CreateConstInt(
                            IntTy: int64Type,
                            N: b
                                ? 1u
                                : 0u,
                            SignExtend: false);
                    case char ch:
                        return LLVMValueRef.CreateConstInt(
                            IntTy: int64Type,
                            N: ch,
                            SignExtend: false);
                    case string s:
                        {
                            // i64 ml_str_intern(i8*)
                            LLVMValueRef intern = EnsureBuiltin(
                                module: module,
                                name: "ml_str_intern",
                                paramTypes: new[]
                                {
                                    LLVMTypeRef.CreatePointer(
                                        ElementType: LLVMTypeRef.Int8,
                                        AddressSpace: 0)
                                },
                                int64Type: int64Type);

                            // Force i8* by GEP: getelementptr inbounds ([N x i8], ptr @g, 0, 0)
                            LLVMValueRef ptr = BuildCStringPtr(
                                module: module,
                                builder: builder,
                                s: s);

                            Span<LLVMValueRef> argv = [ptr];

                            return builder.BuildCall2(
                                Ty: LLVMTypeRef.CreateFunction(
                                    ReturnType: int64Type,
                                    ParamTypes: new[]
                                    {
                                        LLVMTypeRef.CreatePointer(
                                            ElementType: LLVMTypeRef.Int8,
                                            AddressSpace: 0)
                                    },
                                    IsVarArg: false),
                                Fn: intern,
                                Args: argv,
                                Name: "intern");
                        }
                    default:
                        throw new NotSupportedException($"LLVM JIT: const {constant.Value?.GetType().Name}");
                }
            case VReg vreg:
                return builder.BuildLoad2(
                    Ty: int64Type,
                    PointerVal: registerSlots[vreg.Id],
                    Name: $"ld{vreg.Id}");
            default:
                throw new NotSupportedException($"LLVM JIT: operand {operand.GetType().Name}");
        }
    }

    private static void EmitMove(
        LLVMModuleRef module,
        LLVMBuilderRef builder,
        LLVMTypeRef int64Type,
        Dictionary<int, LLVMValueRef> registerSlots,
        HashSet<int> boolRegs,
        Move move)
    {
        LLVMValueRef value = EmitLoadOperandI64(
            module: module,
            builder: builder,
            int64Type: int64Type,
            registerSlots: registerSlots,
            operand: move.Src);

        builder.BuildStore(
            Val: value,
            Ptr: registerSlots[move.Dst.Id]);

        // Propagate boolean-typed marker across moves
        switch (move.Src)
        {
            case Const { Value: bool }:
                boolRegs.Add(move.Dst.Id);

                break;
            case VReg v:
                if (boolRegs.Contains(v.Id))
                {
                    boolRegs.Add(move.Dst.Id);
                }
                else
                {
                    boolRegs.Remove(move.Dst.Id);
                }

                break;
            default:
                boolRegs.Remove(move.Dst.Id);

                break;
        }
    }

    private static void EmitStoreIndex(
        LLVMModuleRef module,
        LLVMBuilderRef builder,
        LLVMTypeRef int64Type,
        Dictionary<int, LLVMValueRef> registerSlots,
        StoreIndex store)
    {
        // long ml_storeidx(long handle, long index, long value)
        LLVMValueRef fn = EnsureBuiltin(
            module: module,
            name: "ml_storeidx",
            paramTypes: new[] { int64Type, int64Type, int64Type },
            int64Type: int64Type);

        LLVMValueRef arr = EmitLoadOperandI64(
            module: module,
            builder: builder,
            int64Type: int64Type,
            registerSlots: registerSlots,
            operand: store.Arr);

        LLVMValueRef idx = EmitLoadOperandI64(
            module: module,
            builder: builder,
            int64Type: int64Type,
            registerSlots: registerSlots,
            operand: store.Index);

        LLVMValueRef val = EmitLoadOperandI64(
            module: module,
            builder: builder,
            int64Type: int64Type,
            registerSlots: registerSlots,
            operand: store.Value);

        Span<LLVMValueRef> argv = [arr, idx, val];
        _ = builder.BuildCall2(
            Ty: LLVMTypeRef.CreateFunction(
                ReturnType: int64Type,
                ParamTypes: new[] { int64Type, int64Type, int64Type },
                IsVarArg: false),
            Fn: fn,
            Args: argv,
            Name: "storeidx");
    }

    private static void EmitUnary(
        LLVMModuleRef module,
        LLVMBuilderRef builder,
        LLVMTypeRef int64Type,
        Dictionary<int, LLVMValueRef> registerSlots,
        HashSet<int> boolRegs,
        Un unary)
    {
        LLVMValueRef x = EmitLoadOperandI64(
            module: module,
            builder: builder,
            int64Type: int64Type,
            registerSlots: registerSlots,
            operand: unary.X);

        LLVMValueRef result = unary.Op switch
        {
            MUnOp.Plus => x,
            MUnOp.Neg => builder.BuildNeg(
                V: x,
                Name: "neg"),
            MUnOp.Not => builder.BuildZExt(
                Val: builder.BuildICmp(
                    Op: LLVMIntPredicate.LLVMIntEQ,
                    LHS: x,
                    RHS: LLVMValueRef.CreateConstInt(
                        IntTy: int64Type,
                        N: 0,
                        SignExtend: false),
                    Name: "not"),
                DestTy: int64Type,
                Name: "zext"),
            _ => throw new NotSupportedException($"LLVM JIT: unop {unary.Op} not supported")
        };

        builder.BuildStore(
            Val: result,
            Ptr: registerSlots[unary.Dst.Id]);

        // Track boolean-typed registers
        if (unary.Op == MUnOp.Not)
        {
            boolRegs.Add(unary.Dst.Id);
        }
        else
        {
            boolRegs.Remove(unary.Dst.Id);
        }
    }

    private static LLVMValueRef EnsureBuiltin(
        LLVMModuleRef module,
        string name,
        ReadOnlySpan<LLVMTypeRef> paramTypes,
        LLVMTypeRef int64Type)
    {
        LLVMValueRef existing = module.GetNamedFunction(name);

        if (existing.Handle != IntPtr.Zero)
        {
            return existing;
        }

        var fnType = LLVMTypeRef.CreateFunction(
            ReturnType: int64Type,
            ParamTypes: paramTypes,
            IsVarArg: false);

        return module.AddFunction(
            Name: name,
            FunctionTy: fnType);
    }

    private static bool IsBuiltinName(
        string name)
    {
        return Builtins.Exists(name);
    }
}
