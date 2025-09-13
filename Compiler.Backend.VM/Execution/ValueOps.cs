using Compiler.Backend.VM.Values;

namespace Compiler.Backend.VM.Execution;

/// <summary>
///     Pure value operations used by JITs and builtins. No VM dependency.
/// </summary>
public static class ValueOps
{
    public static bool AreValuesEqual(
        Value a,
        Value b)
    {
        if (a.Tag != b.Tag)
        {
            return a.Tag switch
            {
                ValueTag.I64 when b.Tag == ValueTag.Char => a.AsInt64() == b.AsChar(),
                ValueTag.Char when b.Tag == ValueTag.I64 => a.AsChar() == b.AsInt64(),
                _ => false
            };
        }

        return a.Tag switch
        {
            ValueTag.Null => true,
            ValueTag.I64 => a.AsInt64() == b.AsInt64(),
            ValueTag.Bool => a.AsBool() == b.AsBool(),
            ValueTag.Char => a.AsChar() == b.AsChar(),
            ValueTag.String => a.AsString() == b.AsString(),
            ValueTag.Array => ReferenceEquals(
                objA: a.Ref,
                objB: b.Ref),
            _ => Equals(
                objA: a.Ref,
                objB: b.Ref)
        };
    }

    public static VmArray Arr(
        Value v)
    {
        return v.AsArray();
    }

    public static Value Get(
        Value[] arr,
        int idx)
    {
        return arr[idx];
    }

    public static long I64(
        Value v)
    {
        return v.AsInt64();
    }

    public static Value Len(
        Value v)
    {
        return v.Tag switch
        {
            ValueTag.String => Value.FromLong(
                v.AsString()
                    .Length),
            ValueTag.Array => Value.FromLong(
                v.AsArray()
                    .Length),
            _ => throw new InvalidOperationException("len: unsupported type")
        };
    }

    public static void Set(
        Value[] arr,
        int idx,
        Value v)
    {
        arr[idx] = v;
    }

    public static bool ToBool(
        Value v)
    {
        return v.Tag switch
        {
            ValueTag.Bool => v.AsBool(),
            ValueTag.Null => false,
            ValueTag.I64 => v.AsInt64() != 0,
            ValueTag.Char => v.AsChar() != '\0',
            ValueTag.String => !string.IsNullOrEmpty(v.AsString()),
            ValueTag.Array => v.AsArray()
                .Length != 0,
            _ => v.Ref is not null
        };
    }
}
