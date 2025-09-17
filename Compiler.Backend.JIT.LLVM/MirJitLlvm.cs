using System.Runtime.InteropServices;

using Compiler.Backend.JIT.Abstractions;
using Compiler.Backend.VM;
using Compiler.Backend.VM.Values;
using Compiler.Frontend.Translation.MIR.Common;

using LLVMSharp.Interop;

using InteropLLVM = LLVMSharp.Interop.LLVM;

namespace Compiler.Backend.JIT.LLVM;

/// <summary>
///     Thin host around LLVM JIT
/// </summary>
public sealed unsafe class MirJitLlvm : IJit
{
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate long MainNoArgRetI64();

    public Value Execute(
        VirtualMachine virtualMachine,
        MirModule mirModule,
        string entryFunctionName)
    {
        var emitter = new LlvmEmitter();
        LLVMModuleRef module = emitter.EmitModule(mirModule);

        return ExecuteModule(
            virtualMachine: virtualMachine,
            module: module,
            entryFunctionName: entryFunctionName);
    }

    public Value ExecuteModule(
        VirtualMachine virtualMachine,
        LLVMModuleRef module,
        string entryFunctionName)
    {
        // Reset static state between runs to avoid leaks across tests
        LlvmBuiltins.ResetForTests();

        // Ensure native target hooks are initialized (idempotent call is fine)
        InteropLLVM.InitializeNativeTarget();
        InteropLLVM.InitializeNativeAsmPrinter();

        // Verify module (sbyte** out parameter)
        sbyte* verifyMessage = null;
        sbyte** pVerifyMessage = &verifyMessage;

        if (InteropLLVM.VerifyModule(
                M: module,
                Action: LLVMVerifierFailureAction.LLVMPrintMessageAction,
                OutMessage: pVerifyMessage) != 0)
        {
            string? message = verifyMessage != null
                ? Marshal.PtrToStringAnsi((nint)verifyMessage)
                : null;

            if (verifyMessage != null)
            {
                InteropLLVM.DisposeMessage(verifyMessage);
            }

            throw new InvalidOperationException($"LLVM module verification failed: {message}");
        }

        // Bind VM and make builtins callable from JITted code (symbol resolution)
        LlvmBuiltins.BindVm(virtualMachine);
        LlvmBuiltins.RegisterSymbols();

        // Create MCJIT engine (expects LLVMOpaqueExecutionEngine** and sbyte**)
        LLVMOpaqueExecutionEngine* opaqueEngine = null;
        LLVMOpaqueExecutionEngine** pOpaqueEngine = &opaqueEngine;
        sbyte* createError = null;
        sbyte** pCreateError = &createError;

        if (InteropLLVM.CreateExecutionEngineForModule(
                OutEE: pOpaqueEngine,
                M: module,
                OutError: pCreateError) != 0)
        {
            string? errorMessage = createError != null
                ? Marshal.PtrToStringAnsi((nint)createError)
                : null;

            if (createError != null)
            {
                InteropLLVM.DisposeMessage(createError);
            }

            throw new InvalidOperationException($"LLVM: failed to create execution engine: {errorMessage}");
        }

        // Capture context if needed (we no longer dispose it explicitly to avoid double-free)
        // LLVMContextRef context = module.Context;
        var engine = new LLVMExecutionEngineRef((nint)opaqueEngine);

        try
        {
            ulong functionAddress = engine.GetFunctionAddress(entryFunctionName);

            if (functionAddress == 0)
            {
                throw new InvalidOperationException("LLVM: failed to get function address");
            }

            IntPtr functionPointer = (nint)functionAddress;
            var compiledEntry = Marshal.GetDelegateForFunctionPointer<MainNoArgRetI64>(functionPointer);

            try
            {
                long result = compiledEntry();

                // If result is a handle to a VM Value, unwrap it; otherwise treat as numeric.
                return LlvmBuiltins.TryResolveHandle(
                    id: result,
                    value: out Value value)
                    ? value
                    : Value.FromLong(result);
            }
            catch
            {
                // If JITted code threw (e.g., assert), make sure to clear any frames/handles
                // to avoid retaining large graphs until the next run.
                LlvmBuiltins.ResetForTests();

                throw;
            }
        }
        finally
        {
            // Dispose engine to free JIT resources; module is owned by the engine.
            // We intentionally do not dispose the context here because MCJIT/module teardown
            // may already release context-owned resources; disposing can double-free.
            InteropLLVM.DisposeExecutionEngine(engine);
        }
    }

    // Optimization pipeline removed for now; we will reintroduce using supported APIs when needed.
}
