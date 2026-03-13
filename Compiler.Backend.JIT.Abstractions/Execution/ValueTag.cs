namespace Compiler.Backend.JIT.Abstractions.Execution;

/// <summary>
///     Discriminant for the shared execution value tagged union.
/// </summary>
public enum ValueTag
{
    Null,
    I64,
    Bool,
    Char,
    String,
    Array,
    Object
}
