namespace Compiler.Runtime.VM;

/// <summary>
///     Compact tagged payload used by the VM.
/// </summary>
public readonly struct VmValue
{
    public static readonly VmValue Null = new VmValue(
        kind: VmValueKind.Null,
        payload: 0);

    private VmValue(
        VmValueKind kind,
        long payload)
    {
        Kind = kind;
        Payload = payload;
    }

    public VmValueKind Kind { get; }

    public long Payload { get; }

    public static VmValue FromBool(
        bool value)
    {
        return new VmValue(
            kind: VmValueKind.Bool,
            payload: value
                ? 1
                : 0);
    }

    public static VmValue FromChar(
        char value)
    {
        return new VmValue(
            kind: VmValueKind.Char,
            payload: value);
    }

    public static VmValue FromHandle(
        int handle)
    {
        if (handle <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(handle));
        }

        return new VmValue(
            kind: VmValueKind.Ref,
            payload: handle);
    }

    public static VmValue FromLong(
        long value)
    {
        return new VmValue(
            kind: VmValueKind.I64,
            payload: value);
    }

    public bool AsBool()
    {
        return Kind == VmValueKind.Bool
            ? Payload != 0
            : throw new InvalidOperationException("value is not bool");
    }

    public char AsChar()
    {
        return Kind == VmValueKind.Char
            ? (char)Payload
            : throw new InvalidOperationException("value is not char");
    }

    public int AsHandle()
    {
        return Kind == VmValueKind.Ref
            ? checked((int)Payload)
            : throw new InvalidOperationException("value is not ref");
    }

    public long AsInt64()
    {
        return Kind == VmValueKind.I64
            ? Payload
            : throw new InvalidOperationException("value is not i64");
    }

    public override string ToString()
    {
        return Kind switch
        {
            VmValueKind.Null => "null",
            VmValueKind.I64 => Payload.ToString(),
            VmValueKind.Bool => Payload != 0
                ? "true"
                : "false",
            VmValueKind.Char => ((char)Payload).ToString(),
            VmValueKind.Ref => $"ref#{Payload}",
            _ => throw new ArgumentOutOfRangeException()
        };
    }
}
