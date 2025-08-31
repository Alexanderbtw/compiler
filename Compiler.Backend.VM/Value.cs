namespace Compiler.Backend.VM;

public enum ValueTag { Null, I64, Bool, Char, String, Array, Object }

public readonly struct Value
{
    public readonly bool B;
    public readonly char C;
    public readonly long I64;

    public readonly static Value Null = new(ValueTag.Null, 0, false, '\0', null);
    public readonly object? Ref;
    public readonly ValueTag Tag;

    private Value(ValueTag tag, long i64, bool b, char c, object? r)
    {
        Tag = tag;
        I64 = i64;
        B = b;
        C = c;
        Ref = r;
    }
    public VmArray AsArr() => Tag == ValueTag.Array
        ? (VmArray)Ref!
        : throw new InvalidOperationException("not array");
    public bool AsBool() => Tag == ValueTag.Bool ? B : throw new InvalidOperationException("not bool");
    public char AsChar() => Tag == ValueTag.Char ? C : throw new InvalidOperationException("not char");

    public long AsLong() => Tag == ValueTag.I64 ? I64 : throw new InvalidOperationException("not i64");
    public string AsStr() => Tag == ValueTag.String
        ? (string)Ref!
        : throw new InvalidOperationException("not string");
    public static Value FromArray(VmArray a) => new(ValueTag.Array, 0, false, '\0', a);
    public static Value FromBool(bool x) => new(ValueTag.Bool, 0, x, '\0', null);
    public static Value FromChar(char x) => new(ValueTag.Char, 0, false, x, null);
    public static Value FromLong(long x) => new(ValueTag.I64, x, false, '\0', null);
    public static Value FromObj(object? o) => o switch
    {
        null => Null,
        string s => FromString(s),
        VmArray a => FromArray(a),
        bool b => FromBool(b),
        char ch => FromChar(ch),
        long n => FromLong(n),
        int n => FromLong(n),
        _ => new Value(ValueTag.Object, 0, false, '\0', o)
    };
    public static Value FromString(string s) => new(ValueTag.String, 0, false, '\0', s);

    public override string ToString() => Tag switch
    {
        ValueTag.Null => "null",
        ValueTag.I64 => I64.ToString(),
        ValueTag.Bool => B ? "true" : "false",
        ValueTag.Char => C.ToString(),
        ValueTag.String => (string)Ref!,
        ValueTag.Array => "[array]",
        _ => Ref?.ToString() ?? "null"
    };
}

public sealed class VmArray
{
    public Value[] Data;
    public VmArray(int n) => Data = new Value[n];

    public int Length => Data.Length;

    public Value this[int i] { get => Data[i]; set => Data[i] = value; }
}
