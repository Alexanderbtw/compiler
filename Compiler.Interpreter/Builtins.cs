using System.Diagnostics;

using Compiler.Frontend.Translation.HIR.Metadata;
using Compiler.Interpreter.Exceptions;

using CommonBuiltins = Compiler.Frontend.Translation.HIR.Metadata.Builtins;

namespace Compiler.Interpreter;

/// <summary>
///     Builtins for the tree-walking interpreter.
///     Mirrors the frontend table and implements a few intrinsics inline for speed.
/// </summary>
public static class Builtins
{
    public static bool TryInvoke(
        string name,
        object?[] args,
        out object? result)
    {
        result = null;

        if (!CommonBuiltins.Table.TryGetValue(
                key: name,
                value: out List<BuiltinDescriptor>? cands) || cands.Count == 0)
        {
            return false; // not a builtin
        }

        // Select a descriptor by arity (no typing here)
        BuiltinDescriptor? d = null;

        foreach (BuiltinDescriptor cand in cands)
        {
            if (cand.Attributes.HasFlag(BuiltinAttr.VarArgs))
            {
                if (args.Length >= cand.MinArity)
                {
                    d = cand;

                    break;
                }
            }
            else
            {
                int max = cand.MaxArity ?? cand.MinArity;

                if (args.Length >= cand.MinArity && args.Length <= max)
                {
                    d = cand;

                    break;
                }
            }
        }

        if (d is null)
        {
            return false; // should be caught by semantics; guard anyway
        }

        switch (d.Lowering)
        {
            case BuiltinLoweringKind.CallRuntime:
                return TryInvokeRuntime(
                    name: name,
                    args: args,
                    result: out result);

            case BuiltinLoweringKind.IntrinsicLen:
                if (args.Length != 1)
                {
                    return false;
                }

                if (args[0] is string s)
                {
                    result = (long)s.Length;

                    return true;
                }

                if (args[0] is Array arr)
                {
                    result = (long)arr.Length;

                    return true;
                }

                throw new RuntimeException("len(...) expects string or array");

            case BuiltinLoweringKind.IntrinsicOrd:
                if (args.Length != 1)
                {
                    return false;
                }

                if (args[0] is char ch)
                {
                    result = (long)ch;

                    return true;
                }

                if (args[0] is string
                    {
                        Length: 1
                    } s2)
                {
                    result = (long)s2[0];

                    return true;
                }

                throw new RuntimeException("ord(...) expects char or 1-length string");

            case BuiltinLoweringKind.IntrinsicChr:
                if (args.Length != 1)
                {
                    return false;
                }

                long code = InterpreterValueOps.ToLong(args[0]);

                if (code < char.MinValue || code > char.MaxValue)
                {
                    throw new RuntimeException("chr(...) code point out of range");
                }

                result = (char)code;

                return true;

            default:
                return false;
        }
    }

    private static bool TryInvokeRuntime(
        string name,
        object?[] args,
        out object? result)
    {
        result = null;

        switch (name)
        {
            case "print":
                BuiltinsCore.PrintLine(
                    tokens: args.Select(a => a is Array
                        ? "[array]"
                        : a?.ToString() ?? "null"));

                return true;
            case "clock_ms":
                result = (long)(Stopwatch.GetTimestamp() * 1000.0 / Stopwatch.Frequency);

                return true;

            case "assert":
                if (args.Length < 1)
                {
                    throw new RuntimeException("assert(cond, msg?) requires at least 1 argument");
                }

                bool cond = InterpreterValueOps.IsTrue(args[0]);

                if (!cond)
                {
                    string msg = args.Length > 1
                        ? args[1]
                            ?.ToString() ?? ""
                        : "assertion failed";

                    throw new RuntimeException($"assert: {msg}");
                }

                return true;

            case "array":
                if (args.Length == 1)
                {
                    long n = InterpreterValueOps.ToLong(args[0]);

                    if (n < 0)
                    {
                        throw new RuntimeException("array length must be non-negative");
                    }

                    result = new object?[n];

                    return true;
                }

                if (args.Length == 2)
                {
                    long n = InterpreterValueOps.ToLong(args[0]);

                    if (n < 0)
                    {
                        throw new RuntimeException("array length must be non-negative");
                    }

                    var arr = new object?[n];

                    for (var i = 0; i < n; i++)
                    {
                        arr[i] = args[1];
                    }

                    result = arr;

                    return true;
                }

                return false;

            // Add domain-specific runtime builtins here

            default:
                return false;
        }
    }
}
