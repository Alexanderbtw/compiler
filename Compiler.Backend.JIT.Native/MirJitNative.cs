using System.Runtime.InteropServices;

using Compiler.Backend.VM;
using Compiler.Backend.VM.Values;
using Compiler.Frontend.Translation.MIR.Common;
using Compiler.Frontend.Translation.MIR.Instructions;
using Compiler.Frontend.Translation.MIR.Operands;

namespace Compiler.Backend.JIT.Native;

public sealed class MirJitNative
{
    private readonly List<ExecMemory> _codeBlocks = new List<ExecMemory>();

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate long RetI64Fn();

    // Symmetric API with CIL JIT
    public Value Execute(
        VirtualMachine vm,
        MirModule module,
        string entry)
    {
        // For now VM is unused (const-return path), but kept for parity and future features.
        return Execute(
            module: module,
            entry: entry);
    }

    public Value Execute(
        MirModule module,
        string entry = "main")
    {
        MirFunction? f = module.Functions.FirstOrDefault(fn => fn.Name == entry);

        if (f is null)
        {
            throw new InvalidOperationException($"entry '{entry}' not found");
        }

        // If the function has exactly one Ret with constant integral value, return it natively
        List<Ret> rets = f
            .Blocks
            .Select(b => b.Terminator)
            .OfType<Ret>()
            .ToList();

        if (rets.Count == 1 && rets[0].Value is not null && TryConstToInt64(
                op: rets[0].Value,
                value: out long cval))
        {
            RetI64Fn fn = CompileReturnConstI64(cval);
            long r = fn();

            return Value.FromLong(r);
        }

        // Not implemented yet for general MIR – will expand incrementally.
        throw new NotSupportedException("Native JIT: general MIR execution not yet implemented");
    }

    private RetI64Fn CompileReturnConstI64(
        long value)
    {
        // x64: mov rax, imm64; ret
        Span<byte> buf = stackalloc byte[10 + 1];
        buf[0] = 0x48; // REX.W
        buf[1] = 0xB8; // mov rax, imm64
        BitConverter
            .GetBytes(value)
            .CopyTo(
                buf.Slice(
                    start: 2,
                    length: 8));

        buf[10] = 0xC3; // ret

        var mem = new ExecMemory((nuint)buf.Length);
        mem.Write(buf);
        _codeBlocks.Add(mem); // keep RX memory alive as long as compiler lives

        return Marshal.GetDelegateForFunctionPointer<RetI64Fn>(mem.Pointer);
    }

    private static bool TryConstToInt64(
        MOperand op,
        out long value)
    {
        value = 0;

        if (op is Const c)
        {
            switch (c.Value)
            {
                case long n:
                    value = n;

                    return true;
                case bool b:
                    value = b
                        ? 1
                        : 0;

                    return true;
                case char ch:
                    value = ch;

                    return true;
            }
        }

        return false;
    }
}
