namespace Compiler.Frontend.Translation.HIR.Metadata;

using static BuiltinAttr;

using static BuiltinLoweringKind;

public static class Builtins
{
    // name -> overloads (future-proof)
    public static readonly Dictionary<string, List<BuiltinDescriptor>> Table = new Dictionary<string, List<BuiltinDescriptor>>
    {
        ["print"] =
        [
            new BuiltinDescriptor(
                Name: "print",
                MinArity: 1,
                MaxArity: null,
                Attributes: VarArgs /* impure by default */,
                ReturnType: SimpleType.Void,
                Lowering: CallRuntime)
        ],
        ["len"] =
        [
            new BuiltinDescriptor(
                Name: "len",
                MinArity: 1,
                MaxArity: 1,
                Attributes: Pure | Foldable | NoThrow,
                ReturnType: SimpleType.Int,
                Lowering: IntrinsicLen)
        ],
        ["ord"] =
        [
            new BuiltinDescriptor(
                Name: "ord",
                MinArity: 1,
                MaxArity: 1,
                Attributes: Pure | Foldable | NoThrow,
                ReturnType: SimpleType.Int,
                Lowering: IntrinsicOrd)
        ],
        ["chr"] =
        [
            new BuiltinDescriptor(
                Name: "chr",
                MinArity: 1,
                MaxArity: 1,
                Attributes: Pure | Foldable, // may throw on range check — do not set NoThrow
                ReturnType: SimpleType.Char,
                Lowering: IntrinsicChr)
        ],
        ["assert"] =
        [
            new BuiltinDescriptor(
                Name: "assert",
                MinArity: 1,
                MaxArity: 2,
                Attributes: None,
                ReturnType: SimpleType.Void,
                Lowering: CallRuntime)
        ],
        ["array"] =
        [
            // array(len)
            new BuiltinDescriptor(
                Name: "array",
                MinArity: 1,
                MaxArity: 1,
                Attributes: None, // allocation: not Pure/Foldable
                ReturnType: SimpleType.Unknown, // effectively returns object?[]; could model as Array(T) for typing
                Lowering: CallRuntime),

            // array(len, init)
            new BuiltinDescriptor(
                Name: "array",
                MinArity: 2,
                MaxArity: 2,
                Attributes: None,
                ReturnType: SimpleType.Unknown,
                Lowering: CallRuntime)
        ],
        ["clock_ms"] =
        [
            new BuiltinDescriptor(
                Name: "clock_ms",
                MinArity: 0,
                MaxArity: 0,
                Attributes: NoThrow, // non-deterministic (not Pure/Foldable), but does not throw
                ReturnType: SimpleType.Int,
                Lowering: CallRuntime)
        ]
    };

    public static bool Exists(
        string name)
    {
        return Table.ContainsKey(name);
    }

    public static (int min, int? max)? GetArity(
        string name)
    {
        return Table.TryGetValue(
            key: name,
            value: out List<BuiltinDescriptor>? list) && list.Count > 0
            ? (list[0].MinArity, list[0].MaxArity) // currently a single “overload”
            : null;
    }

    public static IReadOnlyList<BuiltinDescriptor> GetCandidates(
        string name)
    {
        return Table.TryGetValue(
            key: name,
            value: out List<BuiltinDescriptor>? list)
            ? list
            : Array.Empty<BuiltinDescriptor>();
    }

    public static bool IsPure(
        string name)
    {
        return Table.TryGetValue(
                key: name,
                value: out List<BuiltinDescriptor>? list) &&
            list.Any(d => d.Attributes.HasFlag(Pure));
    }
}
