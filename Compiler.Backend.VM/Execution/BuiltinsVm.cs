using System.Text;

using Compiler.Backend.VM.Values;

namespace Compiler.Backend.VM.Execution;

public static class BuiltinsVm
{
    public static Value Invoke(
        string name,
        VmJitContext ctx,
        Value[] args)
    {
        switch (name)
        {
            case "print":
                return Print(args);
            case "assert":
                return Assert(args);
            case "chr":
                return Chr(args);
            case "ord":
                return Ord(args);
            case "len":
                return Len(args);
            case "array":
                return Array(
                    ctx: ctx,
                    args: args);
            default:
                throw new InvalidOperationException($"unknown builtin '{name}'");
        }
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

        return Value.FromChar(
            (char)args[0]
                .AsInt64());
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

            sb.Append(
                args[i]
                    .ToString());
        }

        Console.WriteLine(sb.ToString());

        return Value.Null;
    }
}
