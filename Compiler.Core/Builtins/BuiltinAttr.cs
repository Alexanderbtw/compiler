namespace Compiler.Core.Builtins;

/// <summary>
///     Flags describing builtin behavior and arity behavior shared by runtime-facing components.
/// </summary>
[Flags]
public enum BuiltinAttr
{
    None = 0,
    Pure = 1 << 0,
    Foldable = 1 << 1,
    NoThrow = 1 << 2,
    VarArgs = 1 << 3,
    Inline = 1 << 4
}
