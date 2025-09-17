using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

using Compiler.Backend.VM;
using Compiler.Backend.VM.Execution;
using Compiler.Backend.VM.Values;
using Compiler.Frontend.Translation.HIR.Metadata;

namespace Compiler.Backend.JIT.LLVM;

using InteropLLVM = LLVMSharp.Interop.LLVM;

/// <summary>
///     Tiny glue that exposes VM builtins and helpers to LLVM MCJIT.
///     Values that aren't plain numbers travel as opaque i64 handles.
///     I also keep a simple handle-frame stack so returns and temporaries don't leak into GC roots.
/// </summary>
internal static unsafe class LlvmBuiltins
{
    private static VmJitContext? Ctx;

    [ThreadStatic]
    private static Stack<List<long>>? HandleFrames;

    private static readonly Dictionary<long, Value> Handles = new Dictionary<long, Value>();

    // Opaque handle storage for VM Values (arrays, later strings)
    private static long NextHandle = 1L << 60; // offset to avoid collisions with small integers

    [ThreadStatic]
    private static List<string>? PrintBuffer;

    private static bool SymbolsRegistered;

    public static void BindVm(
        VirtualMachine vm)
    {
        Ctx = new VmJitContext(vm);
        vm.RegisterExternalRootsProvider(EnumerateRoots);
    }

    public static (int handles, int frameDepth) GetState()
    {
        return (Handles.Count, HandleFrames?.Count ?? 0);
    }

    public static void RegisterSymbols()
    {
        if (SymbolsRegistered)
        {
            return;
        }

        // Register function pointers so JIT can resolve external symbols.
        delegate* unmanaged[Cdecl]<long, long> printI64 = &MlPrintI64;
        delegate* unmanaged[Cdecl]<long> printNl = &MlPrintNl;
        delegate* unmanaged[Cdecl]<long> clockMs = &MlClockMs;
        delegate* unmanaged[Cdecl]<long, long> assert1 = &MlAssert1;
        delegate* unmanaged[Cdecl]<long, long, long> assert2 = &MlAssert2;
        delegate* unmanaged[Cdecl]<long, long> array1 = &MlArray1;
        delegate* unmanaged[Cdecl]<long, long, long> array2 = &MlArray2;
        delegate* unmanaged[Cdecl]<long, long> len1 = &MlLen1;
        delegate* unmanaged[Cdecl]<long, long, long> loadidx = &MlLoadIndex;
        delegate* unmanaged[Cdecl]<long, long, long, long> storeidx = &MlStoreIndex;
        delegate* unmanaged[Cdecl]<long> pushFrame = &MlHandlePushFrame;
        delegate* unmanaged[Cdecl]<long> popFrame = &MlHandlePopFrame;
        delegate* unmanaged[Cdecl]<sbyte*, long> strIntern = &MlStringIntern;
        delegate* unmanaged[Cdecl]<long, long> printAny = &MlPrintAny;
        delegate* unmanaged[Cdecl]<long, long> handleTransfer = &MlHandleTransfer;
        delegate* unmanaged[Cdecl]<long, long> ord = &MlOrd;
        delegate* unmanaged[Cdecl]<long, long> chr = &MlChr;
        delegate* unmanaged[Cdecl]<long, long, long> eq = &MlEq;
        delegate* unmanaged[Cdecl]<long, long, long> ne = &MlNe;
        delegate* unmanaged[Cdecl]<long, long> boolWrap = &MlBoolWrap;
        delegate* unmanaged[Cdecl]<sbyte*, long*, int, long> builtinInvoke = &MlBuiltinInvoke;

        RegisterSymbolAscii(
            name: "ml_print_i64",
            address: printI64);

        RegisterSymbolAscii(
            name: "ml_print_nl",
            address: printNl);

        RegisterSymbolAscii(
            name: "ml_clock_ms",
            address: clockMs);

        RegisterSymbolAscii(
            name: "ml_assert1",
            address: assert1);

        RegisterSymbolAscii(
            name: "ml_assert2",
            address: assert2);

        RegisterSymbolAscii(
            name: "ml_array1",
            address: array1);

        RegisterSymbolAscii(
            name: "ml_array2",
            address: array2);

        RegisterSymbolAscii(
            name: "ml_len1",
            address: len1);

        RegisterSymbolAscii(
            name: "ml_loadidx",
            address: loadidx);

        RegisterSymbolAscii(
            name: "ml_storeidx",
            address: storeidx);

        RegisterSymbolAscii(
            name: "ml_handle_push_frame",
            address: pushFrame);

        RegisterSymbolAscii(
            name: "ml_handle_pop_frame",
            address: popFrame);

        RegisterSymbolAscii(
            name: "ml_str_intern",
            address: strIntern);

        RegisterSymbolAscii(
            name: "ml_print_any",
            address: printAny);

        RegisterSymbolAscii(
            name: "ml_handle_transfer",
            address: handleTransfer);

        RegisterSymbolAscii(
            name: "ml_ord",
            address: ord);

        RegisterSymbolAscii(
            name: "ml_chr",
            address: chr);

        RegisterSymbolAscii(
            name: "ml_eq",
            address: eq);

        RegisterSymbolAscii(
            name: "ml_ne",
            address: ne);

        RegisterSymbolAscii(
            name: "ml_bool_wrap",
            address: boolWrap);

        RegisterSymbolAscii(
            name: "ml_builtin_invoke",
            address: builtinInvoke);

        SymbolsRegistered = true;
    }

    // Test/diagnostic helpers
    public static void ResetForTests()
    {
        Handles.Clear();
        NextHandle = 1L << 60;
        Ctx = null;
        HandleFrames = null;
        PrintBuffer = null;

        // Keep SymbolsRegistered = true; repeated registration is unnecessary
    }

    // Resolve a handle id to a Value (used by host to unwrap returns)
    internal static bool TryResolveHandle(
        long id,
        out Value value)
    {
        return Handles.TryGetValue(
            key: id,
            value: out value);
    }

    private static IEnumerable<Value> EnumerateRoots()
    {
        return Handles.Select(kv => kv.Value);
    }

    private static Value FromMaybeHandle(
        long x)
    {
        return Handles.TryGetValue(
            key: x,
            value: out Value v)
            ? v
            : Value.FromLong(x);
    }

    private static Value LoadHandle(
        long id)
    {
        if (!Handles.TryGetValue(
                key: id,
                value: out Value v))
        {
            throw new InvalidOperationException($"invalid handle {id}");
        }

        return v;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static long MlArray1(
        long n)
    {
        if (Ctx is null)
        {
            throw new InvalidOperationException("LLVM JIT builtins not bound to VM context");
        }

        Value v = BuiltinsVm.Invoke(
            name: "array",
            ctx: Ctx,
            args: [Value.FromLong(n)]);

        return StoreHandle(v);
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static long MlArray2(
        long n,
        long init)
    {
        if (Ctx is null)
        {
            throw new InvalidOperationException("LLVM JIT builtins not bound to VM context");
        }

        Value v = BuiltinsVm.Invoke(
            name: "array",
            ctx: Ctx,
            args: [Value.FromLong(n), Value.FromLong(init)]);

        return StoreHandle(v);
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static long MlAssert1(
        long cond)
    {
        if (Ctx is null)
        {
            throw new InvalidOperationException("LLVM JIT builtins not bound to VM context");
        }

        _ = BuiltinsVm.Invoke(
            name: "assert",
            ctx: Ctx,
            args: [Value.FromLong(cond)]);

        return 0;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static long MlAssert2(
        long cond,
        long msg)
    {
        if (Ctx is null)
        {
            throw new InvalidOperationException("LLVM JIT builtins not bound to VM context");
        }

        Value msgVal = LoadHandle(msg);

        _ = BuiltinsVm.Invoke(
            name: "assert",
            ctx: Ctx,
            args: [Value.FromLong(cond), msgVal]);

        return 0;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static long MlBoolWrap(
        long x)
    {
        return StoreHandle(Value.FromBool(x != 0));
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static long MlBuiltinInvoke(
        sbyte* name,
        long* argv,
        int argc)
    {
        if (Ctx is null)
        {
            throw new InvalidOperationException("LLVM JIT builtins not bound to VM context");
        }

        string builtin = Marshal.PtrToStringUTF8((nint)name) ?? string.Empty;

        // Build Value[] arguments by unwrapping handles when present
        var args = new Value[argc];

        for (int i = 0; i < argc; i++)
        {
            long raw = argv[i];

            if (Handles.TryGetValue(
                    key: raw,
                    value: out Value v))
            {
                args[i] = v;
            }
            else
            {
                args[i] = Value.FromLong(raw);
            }
        }

        Value res = BuiltinsVm.Invoke(
            name: builtin,
            ctx: Ctx,
            args: args);

        return res.Tag switch
        {
            ValueTag.Null => 0,
            ValueTag.I64 => res.AsInt64(),
            ValueTag.Bool => res.AsBool()
                ? 1
                : 0,
            ValueTag.Char => res.AsChar(),
            ValueTag.String or ValueTag.Array or ValueTag.Object => StoreHandle(res),
            _ => StoreHandle(res)
        };
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static long MlChr(
        long code)
    {
        if (code < char.MinValue || code > char.MaxValue)
        {
            throw new InvalidOperationException("chr(...) code point out of range");
        }

        // Return handle to char Value so printing matches CIL; Eq/Ne are handled by ml_eq/ml_ne.
        return StoreHandle(Value.FromChar((char)code));
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static long MlClockMs()
    {
        if (Ctx is null)
        {
            throw new InvalidOperationException("LLVM JIT builtins not bound to VM context");
        }

        Value v = BuiltinsVm.Invoke(
            name: "clock_ms",
            ctx: Ctx,
            args: []);

        return v.AsInt64();
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static long MlEq(
        long a,
        long b)
    {
        return ValueOps.AreValuesEqual(
            a: FromMaybeHandle(a),
            b: FromMaybeHandle(b))
            ? 1
            : 0;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static long MlHandlePopFrame()
    {
        if (HandleFrames is null || HandleFrames.Count == 0)
        {
            return 0;
        }

        List<long> frame = HandleFrames.Pop();

        foreach (long id in frame)
        {
            Handles.Remove(id);
        }

        return 0;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static long MlHandlePushFrame()
    {
        (HandleFrames ??= new Stack<List<long>>()).Push([]);

        return 0;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static long MlHandleTransfer(
        long handle)
    {
        // Not inside a frame or not a handle → nothing to move
        if (HandleFrames is null || HandleFrames.Count == 0 || !Handles.ContainsKey(handle))
        {
            return handle;
        }

        // Remove from current (top) frame if present; move to parent if any
        List<long> top = HandleFrames.Peek();
        int idx = top.IndexOf(handle);
        bool removed = false;

        if (idx >= 0)
        {
            top.RemoveAt(idx);
            removed = true;
        }

        if (removed && HandleFrames.Count >= 2)
        {
            // Temporarily pop to reach parent, then push top back
            List<long> topFrame = HandleFrames.Pop();
            List<long> parent = HandleFrames.Peek();
            parent.Add(handle);
            HandleFrames.Push(topFrame);
        }

        // If no parent, keep the handle in the global map; it will survive pop (intended)
        return handle;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static long MlLen1(
        long handle)
    {
        if (Ctx is null)
        {
            throw new InvalidOperationException("LLVM JIT builtins not bound to VM context");
        }

        Value x = LoadHandle(handle);
        Value v = BuiltinsVm.Invoke(
            name: "len",
            ctx: Ctx,
            args: [x]);

        return v.AsInt64();
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static long MlLoadIndex(
        long handle,
        long index)
    {
        Value x = LoadHandle(handle);
        VmArray arr = x.AsArray();
        int i = checked((int)index);

        if ((uint)i >= (uint)arr.Length)
        {
            throw new IndexOutOfRangeException();
        }

        Value elem = arr[i];

        return elem.Tag switch
        {
            ValueTag.Null => 0,
            ValueTag.I64 => elem.AsInt64(),
            ValueTag.Bool => elem.AsBool()
                ? 1
                : 0,
            ValueTag.Char => elem.AsChar(),
            ValueTag.String or ValueTag.Array or ValueTag.Object => StoreHandle(elem),
            _ => throw new ArgumentOutOfRangeException()
        };
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static long MlNe(
        long a,
        long b)
    {
        return ValueOps.AreValuesEqual(
            a: FromMaybeHandle(a),
            b: FromMaybeHandle(b))
            ? 0
            : 1;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static long MlOrd(
        long x)
    {
        if (Handles.TryGetValue(
                key: x,
                value: out Value v))
        {
            switch (v.Tag)
            {
                case ValueTag.String:
                    {
                        string s = v.AsString();

                        return s.Length > 0
                            ? (long)s[0]
                            : 0;
                    }
                case ValueTag.Char:
                    return v.AsChar();
            }
        }

        // Treat numeric as already a char code
        return x;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static long MlPrintAny(
        long value)
    {
        PrintBuffer ??= new List<string>(4);

        if (Handles.TryGetValue(
                key: value,
                value: out Value v))
        {
            PrintBuffer.Add(v.ToString());
        }
        else
        {
            PrintBuffer.Add(value.ToString());
        }

        return 0;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static long MlPrintI64(
        long value)
    {
        PrintBuffer ??= new List<string>(4);
        PrintBuffer.Add(value.ToString());

        return 0;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static long MlPrintNl()
    {
        if (PrintBuffer is { Count: > 0 })
        {
            BuiltinsCore.PrintLine(PrintBuffer);
            PrintBuffer.Clear();
        }

        return 0;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static long MlStoreIndex(
        long handle,
        long index,
        long value)
    {
        Value x = LoadHandle(handle);
        VmArray arr = x.AsArray();
        int i = checked((int)index);

        if ((uint)i >= (uint)arr.Length)
        {
            throw new IndexOutOfRangeException();
        }

        if (Handles.TryGetValue(
                key: value,
                value: out Value v))
        {
            // Value is a handle → store referenced object
            arr[i] = v;
        }
        else
        {
            // Treat as numeric scalar
            arr[i] = Value.FromLong(value);
        }

        return 0;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static long MlStringIntern(
        sbyte* p)
    {
        string s = Marshal.PtrToStringUTF8((nint)p) ?? string.Empty;

        return StoreHandle(Value.FromString(s));
    }

    private static void RegisterSymbolAscii(
        string name,
        void* address)
    {
        ReadOnlySpan<byte> nameBytes = Encoding.ASCII.GetBytes(name);
        Span<byte> buffer = stackalloc byte[nameBytes.Length + 1];
        nameBytes.CopyTo(buffer);
        buffer[^1] = 0; // null-terminate

        fixed (byte* p = buffer)
        {
            InteropLLVM.AddSymbol(
                symbolName: (sbyte*)p,
                symbolValue: address);
        }
    }

    private static long StoreHandle(
        Value v)
    {
        long id = Interlocked.Increment(ref NextHandle);
        Handles[id] = v;
        (HandleFrames ??= new Stack<List<long>>()).TryPeek(out List<long>? top);

        if (top is null)
        {
            // If no frame is active, create one implicitly to keep GC consistent.
            HandleFrames.Push(top = []);
        }

        top.Add(id);

        return id;
    }
}
