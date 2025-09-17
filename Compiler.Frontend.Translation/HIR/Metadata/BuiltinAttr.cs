namespace Compiler.Frontend.Translation.HIR.Metadata;

/// <summary>
///     Flags describing builtin behavior (purity, foldability, arity behavior).
/// </summary>
[Flags]
public enum BuiltinAttr
{
    None = 0,
    Pure = 1 << 0, // no side effects
    Foldable = 1 << 1, // can be folded with constant arguments
    NoThrow = 1 << 2, // does not throw exceptions
    VarArgs = 1 << 3, // accepts a variable number of arguments
    Inline = 1 << 4 // hint to inline (if implemented)
}
