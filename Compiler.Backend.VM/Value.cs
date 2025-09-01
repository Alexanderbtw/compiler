namespace Compiler.Backend.VM;

public enum ValueTag { Null, I64, Bool, Char, String, Array, Object }

public readonly struct Value
{
    public readonly bool Bool;
    public readonly char Char;
    public readonly long Int64;

    public static readonly Value Null = new Value(
        tag: ValueTag.Null,
        int64: 0,
        b: false,
        c: '\0',
        r: null);

    public readonly object? Ref;
    public readonly ValueTag Tag;

    private Value(
        ValueTag tag,
        long int64,
        bool b,
        char c,
        object? r)
    {
        Tag = tag;
        Int64 = int64;
        Bool = b;
        Char = c;
        Ref = r;
    }
    public VmArray AsArray()
    {
        return Tag == ValueTag.Array
            ? (VmArray)Ref!
            : throw new InvalidOperationException("not array");
    }

    public bool AsBool()
    {
        return Tag == ValueTag.Bool
            ? Bool
            : throw new InvalidOperationException("not bool");
    }

    public char AsChar()
    {
        return Tag == ValueTag.Char
            ? Char
            : throw new InvalidOperationException("not char");
    }

    public long AsInt64()
    {
        return Tag == ValueTag.I64
            ? Int64
            : throw new InvalidOperationException("not i64");
    }
    public string AsString()
    {
        return Tag == ValueTag.String
            ? (string)Ref!
            : throw new InvalidOperationException("not string");
    }

    public static Value FromArray(
        VmArray a)
    {
        return new Value(
            tag: ValueTag.Array,
            int64: 0,
            b: false,
            c: '\0',
            r: a);
    }

    public static Value FromBool(
        bool x)
    {
        return new Value(
            tag: ValueTag.Bool,
            int64: 0,
            b: x,
            c: '\0',
            r: null);
    }

    public static Value FromChar(
        char x)
    {
        return new Value(
            tag: ValueTag.Char,
            int64: 0,
            b: false,
            c: x,
            r: null);
    }

    public static Value FromLong(
        long x)
    {
        return new Value(
            tag: ValueTag.I64,
            int64: x,
            b: false,
            c: '\0',
            r: null);
    }
    public static Value FromObj(
        object? o)
    {
        return o switch
        {
            null => Null,
            string s => FromString(s),
            VmArray a => FromArray(a),
            bool b => FromBool(b),
            char ch => FromChar(ch),
            long n => FromLong(n),
            int n => FromLong(n),
            _ => new Value(
                tag: ValueTag.Object,
                int64: 0,
                b: false,
                c: '\0',
                r: o)
        };
    }

    public static Value FromString(
        string s)
    {
        return new Value(
            tag: ValueTag.String,
            int64: 0,
            b: false,
            c: '\0',
            r: s);
    }

    public override string ToString()
    {
        return Tag switch
        {
            ValueTag.Null => "null",
            ValueTag.I64 => Int64.ToString(),
            ValueTag.Bool => Bool
                ? "true"
                : "false",
            ValueTag.Char => Char.ToString(),
            ValueTag.String => (string)Ref!,
            ValueTag.Array => "[array]",
            _ => Ref?.ToString() ?? "null"
        };
    }
}

public sealed class VmArray
{
    public Value[] Data;
    public VmArray(
        int n)
    {
        Data = new Value[n];
    }

    public int Length => Data.Length;

    public Value this[
        int i] { get => Data[i]; set => Data[i] = value; }
}
