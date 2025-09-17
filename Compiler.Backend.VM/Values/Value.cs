namespace Compiler.Backend.VM.Values;

/// <summary>
///     Tiny tagged union for VM values.
///     Keeps primitives inline and uses <see cref="Ref" /> for strings/arrays/objects.
/// </summary>
public readonly struct Value
{
    private readonly bool _bool;
    private readonly char _char;
    private readonly long _int64;

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
        _int64 = int64;
        _bool = b;
        _char = c;
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
            ? _bool
            : throw new InvalidOperationException("not bool");
    }

    public char AsChar()
    {
        return Tag == ValueTag.Char
            ? _char
            : throw new InvalidOperationException("not char");
    }

    public long AsInt64()
    {
        return Tag == ValueTag.I64
            ? _int64
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
            ValueTag.I64 => _int64.ToString(),
            ValueTag.Bool => _bool
                ? "true"
                : "false",
            ValueTag.Char => _char.ToString(),
            ValueTag.String => (string)Ref!,
            ValueTag.Array => "[array]",
            _ => Ref?.ToString() ?? "null"
        };
    }
}
