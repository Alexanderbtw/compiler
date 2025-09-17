namespace Compiler.Backend.VM.Values;

/// <summary>
///     Discriminant for the Value tagged union.
/// </summary>
public enum ValueTag { Null, I64, Bool, Char, String, Array, Object }
