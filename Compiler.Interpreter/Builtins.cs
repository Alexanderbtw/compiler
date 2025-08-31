using System;
using System.Collections.Generic;
using System.Linq;

using Compiler.Frontend.Translation.HIR.Metadata;
using Compiler.Interpreter.Exceptions;

using CommonBuiltins = Compiler.Frontend.Translation.HIR.Metadata.Builtins;

namespace Compiler.Interpreter;

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
            return false; // не builtin
        }

        // Подбор подходящего дескриптора по арности (без типизации)
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
            return false; // семантика должна была отловить, но на всякий случай
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

                if (args[0] is string s2 && s2.Length == 1)
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

                long code = ToLong(args[0]);

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

    private static bool IsTrue(
        object? v)
    {
        return v switch
        {
            bool b => b,
            long n => n != 0,
            _ => v != null
        };
    }

    private static long ToLong(
        object? v)
    {
        return v switch
        {
            long n => n,
            bool b => b
                ? 1
                : 0,
            null => throw new RuntimeException("null used where integer expected"),
            _ => throw new RuntimeException($"cannot use {v.GetType().Name} as integer")
        };
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
                // print(x, y, z ...) → пишет в stdout строку с пробелами
                Console.WriteLine(
                    string.Join(
                        separator: " ",
                        values: args.Select(a => a?.ToString() ?? "null")));

                return true;

            case "assert":
                if (args.Length < 1)
                {
                    throw new RuntimeException("assert(cond, msg?) requires at least 1 argument");
                }

                bool cond = IsTrue(args[0]);

                if (!cond)
                {
                    string msg = args.Length > 1
                        ? args[1]
                            ?.ToString() ?? ""
                        : "assertion failed";

                    throw new RuntimeException($"assert: {msg}");
                }

                return true;

            case "clock_ms":
                result = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

                return true;

            case "array":
                if (args.Length == 1)
                {
                    long n = ToLong(args[0]);

                    if (n < 0)
                    {
                        throw new RuntimeException("array length must be non-negative");
                    }

                    result = new object?[n];

                    return true;
                }

                if (args.Length == 2)
                {
                    long n = ToLong(args[0]);

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

            // Добавляй свои domain-specific runtime builtins здесь

            default:
                return false;
        }
    }
}
