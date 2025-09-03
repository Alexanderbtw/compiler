using System.Diagnostics;
using System.Text;

using Compiler.Backend.VM.Values;

namespace Compiler.Backend.VM.Execution;

public static class BuiltinsVm
{
    public static Value Invoke(
        string name,
        ReadOnlySpan<Value> args)
    {
        return name switch
        {
            "print" => Print(args),
            "clock_ms" => ClockMs(args),
            "assert" => Assert(args),
            "chr" => Chr(args),
            "ord" => Ord(args),
            _ => throw new InvalidOperationException($"unknown builtin '{name}'")
        };
    }

    private static Value Assert(
        ReadOnlySpan<Value> args)
    {
        if (args.Length != 1)
        {
            throw new InvalidOperationException("assert(x) expects 1 arg");
        }

        if (!args[0]
                .AsBool())
        {
            throw new InvalidOperationException("assert failed");
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

        return Value.FromChar(
            (char)args[0]
                .AsInt64());
    }
    private static Value ClockMs(
        ReadOnlySpan<Value> args)
    {
        if (args.Length != 0)
        {
            throw new InvalidOperationException("clock_ms() expects 0 args");
        }

        return Value.FromLong(Stopwatch.GetTimestamp() * 1000 / Stopwatch.Frequency);
    }

    private static Value Ord(
        ReadOnlySpan<Value> args)
    {
        if (args.Length != 1)
        {
            throw new InvalidOperationException("ord(c) expects 1 arg");
        }

        return args[0].Tag == ValueTag.Char
            ? Value.FromLong(
                args[0]
                    .AsChar())
            : Value.FromLong(
                args[0]
                    .AsString()[0]);
    }

    private static Value Print(
        ReadOnlySpan<Value> args)
    {
        var sb = new StringBuilder();
        for (int i = 0; i < args.Length; i++)
        {
            if (i > 0)
            {
                sb.Append(' ');
            }
            sb.Append(args[i].ToString());
        }

        Console.WriteLine(sb.ToString());

        return Value.Null;
    }
}
