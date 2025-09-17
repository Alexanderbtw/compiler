using System.Diagnostics;

using Compiler.Backend.VM.Values;
using Compiler.Frontend.Translation.HIR.Metadata;
using Compiler.Frontend.Translation.HIR.Metadata;

namespace Compiler.Backend.VM.Execution;

/// <summary>
///     VM builtins used by both JITs.
/// </summary>
public static class BuiltinsVm
{
    public static Value Invoke(
        string name,
        VmJitContext ctx,
        Value[] args)
    {
        // Validate against the frontend builtin catalog to keep names/arity in sync
        IReadOnlyList<BuiltinDescriptor> cands = Builtins.GetCandidates(name);

        if (cands.Count == 0)
        {
            throw new InvalidOperationException($"unknown builtin '{name}'");
        }

        bool arityOk = cands.Any(d =>
            (d.Attributes.HasFlag(BuiltinAttr.VarArgs) && args.Length >= d.MinArity) ||
            (!d.Attributes.HasFlag(BuiltinAttr.VarArgs) && args.Length >= d.MinArity && args.Length <= (d.MaxArity ?? d.MinArity)));

        if (!arityOk)
        {
            throw new InvalidOperationException($"builtin '{name}' arity mismatch: got {args.Length}");
        }

        return name switch
        {
            "print" => Print(args),
            "assert" => Assert(args),
            "chr" => Chr(args),
            "ord" => Ord(args),
            "len" => Len(args),
            "array" => Array(
                ctx: ctx,
                args: args),
            "clock_ms" => ClockMs(),
            _ => throw new InvalidOperationException($"unknown builtin '{name}'")
        };
    }

    private static Value Array(
        VmJitContext ctx,
        ReadOnlySpan<Value> args)
    {
        if (args.Length is not (1 or 2))
        {
            throw new InvalidOperationException("array(n[, init]) expects 1 or 2 args");
        }

        int n = (int)args[0]
            .AsInt64();

        if (n < 0)
        {
            throw new InvalidOperationException("array length must be non-negative");
        }

        VmArray arr = ctx.AllocArray(n);

        if (args.Length == 2 && n > 0)
        {
            Value init = args[1];

            for (int i = 0; i < n; i++)
            {
                arr[i] = init;
            }
        }

        return Value.FromArray(arr);
    }

    private static Value Assert(
        ReadOnlySpan<Value> args)
    {
        if (args.Length < 1)
        {
            throw new InvalidOperationException("assert(cond, msg?) requires at least 1 argument");
        }

        bool cond = ValueOps.ToBool(args[0]);

        if (!cond)
        {
            string msg = args.Length > 1
                ? args[1]
                    .ToString()
                : "assertion failed";

            throw new InvalidOperationException($"assert: {msg}");
        }

        return Value.Null;
    }
    private static Value Chr(
        ReadOnlySpan<Value> args)
    {
        if (args.Length != 1)
        {
            throw new InvalidOperationException("chr(x) expects 1 arg");
        }

        long code = args[0]
            .AsInt64();

        if (code < char.MinValue || code > char.MaxValue)
        {
            // Match interpreter range check
            throw new InvalidOperationException("chr(...) code point out of range");
        }

        return Value.FromChar((char)code);
    }

    private static Value ClockMs()
    {
        long ms = (long)(Stopwatch.GetTimestamp() * 1000.0 / Stopwatch.Frequency);

        return Value.FromLong(ms);
    }
    private static Value Len(
        ReadOnlySpan<Value> args)
    {
        if (args.Length != 1)
        {
            throw new InvalidOperationException("len(x) expects 1 arg");
        }

        return ValueOps.Len(args[0]);
    }

    private static Value Ord(
        ReadOnlySpan<Value> args)
    {
        if (args.Length != 1)
        {
            throw new InvalidOperationException("ord(c) expects 1 arg");
        }

        // Match interpreter: accepts char or 1-length string
        if (args[0].Tag == ValueTag.Char)
        {
            return Value.FromLong(
                args[0]
                    .AsChar());
        }

        if (args[0].Tag == ValueTag.String)
        {
            string s = args[0]
                .AsString();

            if (s.Length != 1)
            {
                throw new InvalidOperationException("ord(...) expects char or 1-length string");
            }

            return Value.FromLong(s[0]);
        }

        throw new InvalidOperationException("ord(...) expects char or 1-length string");
    }

    private static Value Print(
        ReadOnlySpan<Value> args)
    {
        BuiltinsCore.PrintLine(
            tokens: args
                .ToArray()
                .Select(v => v.ToString()));

        return Value.Null;
    }
}
