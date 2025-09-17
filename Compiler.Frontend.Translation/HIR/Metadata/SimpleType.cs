namespace Compiler.Frontend.Translation.HIR.Metadata;

/// <summary>
///     Minimal type sketch used in builtin metadata and diagnostics.
///     This is intentionally not a full type system.
/// </summary>
public enum SimpleType { Unknown, Int, Bool, Char, String, Void /*, Array(T), ... */ }
