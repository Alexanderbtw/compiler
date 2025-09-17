namespace Compiler.Frontend.Translation.HIR.Semantic.Exceptions;

/// <summary>
///     Thrown when semantic rules are violated in HIR (e.g., missing function, bad builtin usage).
/// </summary>
public sealed class SemanticException(
    string msg) : Exception(msg);
