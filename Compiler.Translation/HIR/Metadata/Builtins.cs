namespace Compiler.Translation.HIR.Metadata;

using static BuiltinAttr;

using static BuiltinLoweringKind;

public static class Builtins
{
    // name -> overloads (на будущее)
    public readonly static Dictionary<string, List<BuiltinDescriptor>> Table = new()
    {
        ["print"] =
        [
            new BuiltinDescriptor(
                "print",
                1,
                null,
                VarArgs /* impure по умолчанию */,
                SimpleType.Void,
                Lowering: CallRuntime)
        ],
        ["len"] =
        [
            new BuiltinDescriptor(
                "len",
                1,
                1,
                Pure | Foldable | NoThrow,
                SimpleType.Int,
                Lowering: IntrinsicLen)
        ],
        ["ord"] =
        [
            new BuiltinDescriptor(
                "ord",
                1,
                1,
                Pure | Foldable | NoThrow,
                SimpleType.Int,
                Lowering: IntrinsicOrd)
        ],
        ["chr"] =
        [
            new BuiltinDescriptor(
                "chr",
                1,
                1,
                Pure | Foldable, // может бросать при проверке диапазона — NoThrow не ставим
                SimpleType.Char,
                Lowering: IntrinsicChr)
        ],
        ["assert"] =
        [
            new BuiltinDescriptor("assert", 1, 2, None, SimpleType.Void, Lowering: CallRuntime)
        ],
        ["array"] =
        [
            // array(len)
            new BuiltinDescriptor(
                "array",
                1,
                1,
                None, // аллокация: не Pure/Foldable
                SimpleType.Unknown, // фактически возвращает object?[]; для типизации можно завести Array(T)
                Lowering: CallRuntime),
            // array(len, init)
            new BuiltinDescriptor(
                "array",
                2,
                2,
                None,
                SimpleType.Unknown,
                Lowering: CallRuntime)
        ],
        ["clock_ms"] =
        [
            new BuiltinDescriptor(
                "clock_ms",
                0,
                0,
                NoThrow, // не детерминирован (не Pure, не Foldable), но и не бросает
                SimpleType.Int,
                Lowering: CallRuntime)
        ]
    };

    public static bool Exists(string name) => Table.ContainsKey(name);

    public static (int min, int? max)? GetArity(string name) =>
        Table.TryGetValue(name, out List<BuiltinDescriptor>? list) && list.Count > 0
            ? (list[0].MinArity, list[0].MaxArity) // пока одна “перегрузка”
            : null;

    public static IReadOnlyList<BuiltinDescriptor> GetCandidates(string name) =>
        Table.TryGetValue(name, out List<BuiltinDescriptor>? list)
            ? list
            : Array.Empty<BuiltinDescriptor>();

    public static bool IsPure(string name) => Table.TryGetValue(name, out List<BuiltinDescriptor>? list) &&
                                              list.Any(d => d.Attributes.HasFlag(Pure));
}
