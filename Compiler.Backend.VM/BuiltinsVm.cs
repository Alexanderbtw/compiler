using System;
using System.Diagnostics;

namespace Compiler.Backend.VM;

public static class BuiltinsVm
{
    public static Value Invoke(string name, ReadOnlySpan<Value> args)
    {
        return name switch
        {
            "print"     => Print(args),
            "array"     => Array(args),
            "len"       => Len(args),
            "clock_ms"  => ClockMs(args),
            "assert"    => Assert(args),
            "chr"       => Chr(args),
            "ord"       => Ord(args),
            _ => throw new InvalidOperationException($"unknown builtin '{name}'")
        };
    }

    private static Value Print(ReadOnlySpan<Value> args)
    {
        // ВАЖНО: не добавляем лишних пробелов! Просто конкат выводов подряд.
        foreach (Value arg in args)
            Console.WriteLine(arg.ToString());
        return Value.Null;
    }
    private static Value Array(ReadOnlySpan<Value> args)
    {
        if (args.Length != 1) throw new InvalidOperationException("array(n) expects 1 arg");
        var n = checked((int)args[0].AsLong());
        return Value.FromArray(new VmArray(n));
    }
    private static Value Len(ReadOnlySpan<Value> args)
    {
        if (args.Length != 1) throw new InvalidOperationException("len(x) expects 1 arg");
        var x = args[0];
        return x.Tag switch
        {
            ValueTag.String => Value.FromLong(x.AsStr().Length),
            ValueTag.Array  => Value.FromLong(x.AsArr().Length),
            _ => throw new InvalidOperationException("len: unsupported type")
        };
    }
    private static Value ClockMs(ReadOnlySpan<Value> args)
    {
        if (args.Length != 0) throw new InvalidOperationException("clock_ms() expects 0 args");
        // монотонное время в миллисекундах
        return Value.FromLong(Stopwatch.GetTimestamp() * 1000 / Stopwatch.Frequency);
    }
    private static Value Assert(ReadOnlySpan<Value> args)
    {
        if (args.Length != 1) throw new InvalidOperationException("assert(x) expects 1 arg");
        if (!args[0].AsBool()) throw new InvalidOperationException("assert failed");
        return Value.Null;
    }
    private static Value Chr(ReadOnlySpan<Value> args)
    {
        if (args.Length != 1) throw new InvalidOperationException("chr(x) expects 1 arg");
        return Value.FromChar((char)args[0].AsLong());
    }
    private static Value Ord(ReadOnlySpan<Value> args)
    {
        if (args.Length != 1) throw new InvalidOperationException("ord(c) expects 1 arg");
        return args[0].Tag == ValueTag.Char
            ? Value.FromLong(args[0].AsChar())
            : Value.FromLong(args[0].AsStr()[0]);
    }
}