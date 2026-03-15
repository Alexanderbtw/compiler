using System.Diagnostics;

using Compiler.Core.Builtins;

namespace Compiler.Runtime.VM.Execution;

/// <summary>
///     Runtime builtins executed by the register VM.
/// </summary>
public static class VmBuiltins
{
    public static VmValue Invoke(
        string name,
        IVmExecutionRuntime vm,
        VmValue[] args)
    {
        ArgumentNullException.ThrowIfNull(args);

        return Invoke(
            name: name,
            vm: vm,
            args: (ReadOnlySpan<VmValue>)args);
    }

    public static VmValue Invoke(
        string name,
        IVmExecutionRuntime vm,
        ReadOnlySpan<VmValue> args)
    {
        IReadOnlyList<BuiltinSignature> candidates = BuiltinCatalog.GetCandidates(name);

        if (candidates.Count == 0)
        {
            throw new InvalidOperationException($"unknown builtin '{name}'");
        }

        var arityOk = false;

        foreach (BuiltinSignature descriptor in candidates)
        {
            bool matches = descriptor.Attributes.HasFlag(BuiltinAttr.VarArgs)
                ? args.Length >= descriptor.MinArity
                : args.Length >= descriptor.MinArity && args.Length <= (descriptor.MaxArity ?? descriptor.MinArity);

            if (!matches)
            {
                continue;
            }

            arityOk = true;

            break;
        }

        if (!arityOk)
        {
            throw new InvalidOperationException($"builtin '{name}' arity mismatch: got {args.Length}");
        }

        return name switch
        {
            "print" => Print(
                vm: vm,
                args: args),
            "assert" => Assert(
                vm: vm,
                args: args),
            "chr" => Chr(args),
            "ord" => Ord(
                vm: vm,
                args: args),
            "len" => Len(
                vm: vm,
                args: args),
            "array" => Array(
                vm: vm,
                args: args),
            "clock_ms" => ClockMs(),
            _ => throw new InvalidOperationException($"unknown builtin '{name}'")
        };
    }

    private static VmValue Array(
        IVmExecutionRuntime vm,
        ReadOnlySpan<VmValue> args)
    {
        if (args.Length is not (1 or 2))
        {
            throw new InvalidOperationException("array(n[, init]) expects 1 or 2 args");
        }

        var length = (int)args[0]
            .AsInt64();

        if (length < 0)
        {
            throw new InvalidOperationException("array length must be non-negative");
        }

        VmValue arrayValue = vm.AllocateArray(length);
        int handle = arrayValue.AsHandle();

        if (args.Length == 2 && length > 0)
        {
            for (var index = 0; index < length; index++)
            {
                vm.SetArrayElement(
                    handle: handle,
                    index: index,
                    value: args[1]);
            }
        }

        return arrayValue;
    }

    private static VmValue Assert(
        IVmExecutionRuntime vm,
        ReadOnlySpan<VmValue> args)
    {
        if (args.Length < 1)
        {
            throw new InvalidOperationException("assert(cond, msg?) requires at least 1 argument");
        }

        bool condition = VmValueOps.ToBool(
            value: args[0],
            vm: vm);

        if (!condition)
        {
            string message = args.Length > 1
                ? vm.FormatValue(args[1])
                : "assertion failed";

            throw new InvalidOperationException($"assert: {message}");
        }

        return VmValue.Null;
    }

    private static VmValue Chr(
        ReadOnlySpan<VmValue> args)
    {
        if (args.Length != 1)
        {
            throw new InvalidOperationException("chr(x) expects 1 arg");
        }

        long code = args[0]
            .AsInt64();

        if (code < char.MinValue || code > char.MaxValue)
        {
            throw new InvalidOperationException("chr(...) code point out of range");
        }

        return VmValue.FromChar((char)code);
    }

    private static VmValue ClockMs()
    {
        var milliseconds = (long)(Stopwatch.GetTimestamp() * 1000.0 / Stopwatch.Frequency);

        return VmValue.FromLong(milliseconds);
    }

    private static VmValue Len(
        IVmExecutionRuntime vm,
        ReadOnlySpan<VmValue> args)
    {
        if (args.Length != 1)
        {
            throw new InvalidOperationException("len(x) expects 1 arg");
        }

        return VmValueOps.Len(
            value: args[0],
            vm: vm);
    }

    private static VmValue Ord(
        IVmExecutionRuntime vm,
        ReadOnlySpan<VmValue> args)
    {
        if (args.Length != 1)
        {
            throw new InvalidOperationException("ord(c) expects 1 arg");
        }

        if (args[0].Kind == VmValueKind.Char)
        {
            return VmValue.FromLong(
                args[0]
                    .AsChar());
        }

        if (args[0].Kind == VmValueKind.Ref &&
            vm.GetHeapObjectKind(
                args[0]
                    .AsHandle()) == HeapObjectKind.String)
        {
            string text = vm.GetString(
                args[0]
                    .AsHandle());

            if (text.Length != 1)
            {
                throw new InvalidOperationException("ord(...) expects char or 1-length string");
            }

            return VmValue.FromLong(text[0]);
        }

        throw new InvalidOperationException("ord(...) expects char or 1-length string");
    }

    private static VmValue Print(
        IVmExecutionRuntime vm,
        ReadOnlySpan<VmValue> args)
    {
        BuiltinsCore.PrintLine(
            args
                .ToArray()
                .Select(vm.FormatValue));

        return VmValue.Null;
    }
}
