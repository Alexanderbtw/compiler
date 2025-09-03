using System.Diagnostics;

namespace Compiler.Backend.CLR.Runtime;

public static class BuiltinsRuntime
{
    public static object? Invoke(
        string name,
        object?[] args)
    {
        return name switch
        {
            "print" => Print(args),
            "array" => ArrayMake(args),
            "assert" => Assert(args),
            "clock_ms" => Stopwatch.GetTimestamp() * 1000L / Stopwatch.Frequency,
            "len" => Len(args),
            "ord" => Ord(args),
            "chr" => Chr(args),
            _ => throw new MissingMethodException($"builtin '{name}' not implemented in runtime")
        };
    }

    private static object ArrayMake(
        object?[] args)
    {
        if (args.Length is < 1 or > 2)
        {
            throw new ArgumentException("array(len [, init])");
        }

        long n64 = args[0] is long n
            ? n
            : throw new ArgumentException("array length must be int");

        if (n64 < 0)
        {
            throw new ArgumentException("array length must be non-negative");
        }

        n = checked((int)n64);
        object?[] a = new object?[n];

        if (args.Length == 2)
        {
            Array.Fill(
                array: a,
                value: args[1]);
        }

        return a;
    }

    private static object? Assert(
        object?[] args)
    {
        if (args.Length is < 1 or > 2)
        {
            throw new ArgumentException("assert(cond, msg?)");
        }

        bool cond = Runtime.ToBool(args[0]);

        if (!cond)
        {
            string msg = args.Length > 1
                ? args[1]
                    ?.ToString() ?? ""
                : "assertion failed";

            throw new Exception($"assert: {msg}");
        }

        return null;
    }

    private static object Chr(
        object?[] args)
    {
        if (args.Length != 1)
        {
            throw new ArgumentException("chr(i)");
        }

        long code = args[0] is long n
            ? n
            : throw new ArgumentException("chr expects int");

        if (code < char.MinValue || code > char.MaxValue)
        {
            throw new ArgumentOutOfRangeException("chr code point out of range");
        }

        return (char)code;
    }

    private static object Len(
        object?[] args)
    {
        if (args.Length != 1)
        {
            throw new ArgumentException("len(x)");
        }

        switch (args[0])
        {
            case string s:
                return s.Length;
            case Array arr:
                return (long)arr.Length;
            default:
                throw new ArgumentException("len expects string or array");
        }
    }

    private static object Ord(
        object?[] args)
    {
        if (args.Length != 1)
        {
            throw new ArgumentException("ord(c)");
        }

        return args[0] switch
        {
            char ch => ch,
            string
            {
                Length: 1
            } s => (long)s[0],
            _ => throw new ArgumentException("ord expects char or 1-length string")
        };
    }

    private static object? Print(
        object?[] args)
    {
        Console.WriteLine(string.Concat(args.Select(a => a?.ToString() ?? "null")));

        return null;
    }
}
