using System;
using System.Collections.Generic;

using Compiler.Frontend.Interpretation.Exceptions;

namespace Compiler.Frontend.Services;

public static class Builtins
{
    public readonly static Dictionary<string, Info> Table = new()
    {
        ["print"] = new Info("print", -1, Print),
        ["clock_ms"] = new Info("clock_ms", 0, _ => Environment.TickCount64),
        ["array"] = new Info("array", 1, a => new object?[Convert.ToInt32(a[0])])
    };

    public static bool Exists(string name) => Table.ContainsKey(name);
    public static Info Get(string name) => Table[name];

    public static int GetArity(string name) => Get(name).Arity;

    private static object? Print(object?[] xs)
    {
        Console.WriteLine(string.Join("", xs));
        return null;
    }

    public static bool TryInvoke(string name, object?[] args, out object? result)
    {
        if (!Table.TryGetValue(name, out Info? info))
        {
            result = null;
            return false;
        }
        if (info.Arity >= 0 && info.Arity != args.Length)
            throw new RuntimeException($"'{name}' expects {info.Arity} args, got {args.Length}");
        result = info.Impl(args);
        return true;
    }

    public sealed record Info(
        string Name,
        int Arity,
        Func<object?[], object?> Impl);
}
