namespace Compiler.Core.Builtins;

/// <summary>
///     Minimal runtime-facing builtin signature metadata.
/// </summary>
/// <param name="Name">Builtin name.</param>
/// <param name="MinArity">Minimum supported arity.</param>
/// <param name="MaxArity">Maximum supported arity. <see langword="null" /> means unbounded.</param>
/// <param name="Attributes">Behavioral attributes.</param>
public sealed record BuiltinSignature(
    string Name,
    int MinArity,
    int? MaxArity,
    BuiltinAttr Attributes);
