namespace Compiler.Core.Builtins;

/// <summary>
///     Shared builtin catalog used by runtime-facing layers and bytecode-backed backends.
/// </summary>
public static class BuiltinCatalog
{
    private static readonly Dictionary<string, IReadOnlyList<BuiltinSignature>> Table = new(StringComparer.Ordinal)
    {
        ["print"] =
        [
            new BuiltinSignature(
                Name: "print",
                MinArity: 1,
                MaxArity: null,
                Attributes: BuiltinAttr.VarArgs)
        ],
        ["len"] =
        [
            new BuiltinSignature(
                Name: "len",
                MinArity: 1,
                MaxArity: 1,
                Attributes: BuiltinAttr.Pure | BuiltinAttr.Foldable | BuiltinAttr.NoThrow)
        ],
        ["ord"] =
        [
            new BuiltinSignature(
                Name: "ord",
                MinArity: 1,
                MaxArity: 1,
                Attributes: BuiltinAttr.Pure | BuiltinAttr.Foldable | BuiltinAttr.NoThrow)
        ],
        ["chr"] =
        [
            new BuiltinSignature(
                Name: "chr",
                MinArity: 1,
                MaxArity: 1,
                Attributes: BuiltinAttr.Pure | BuiltinAttr.Foldable)
        ],
        ["assert"] =
        [
            new BuiltinSignature(
                Name: "assert",
                MinArity: 1,
                MaxArity: 2,
                Attributes: BuiltinAttr.None)
        ],
        ["array"] =
        [
            new BuiltinSignature(
                Name: "array",
                MinArity: 1,
                MaxArity: 2,
                Attributes: BuiltinAttr.None)
        ],
        ["clock_ms"] =
        [
            new BuiltinSignature(
                Name: "clock_ms",
                MinArity: 0,
                MaxArity: 0,
                Attributes: BuiltinAttr.NoThrow)
        ]
    };

    /// <summary>
    ///     Returns whether the builtin exists.
    /// </summary>
    /// <param name="name">Builtin name.</param>
    /// <returns><see langword="true" /> when the builtin is known.</returns>
    public static bool Exists(
        string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return Table.ContainsKey(name);
    }

    /// <summary>
    ///     Gets candidate signatures for a builtin.
    /// </summary>
    /// <param name="name">Builtin name.</param>
    /// <returns>Known signatures or an empty collection.</returns>
    public static IReadOnlyList<BuiltinSignature> GetCandidates(
        string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        return Table.TryGetValue(
            key: name,
            value: out IReadOnlyList<BuiltinSignature>? signatures)
            ? signatures
            : Array.Empty<BuiltinSignature>();
    }
}
