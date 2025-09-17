using System;

namespace Compiler.Interpreter.Exceptions;

/// <summary>
///     Interpreter runtime error (e.g., bad indexing, arity mismatch, etc.).
/// </summary>
public sealed class RuntimeException(
    string msg) : Exception(msg);
