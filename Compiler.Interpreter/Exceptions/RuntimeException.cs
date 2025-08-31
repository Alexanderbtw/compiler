using System;

namespace Compiler.Interpreter.Exceptions;

public sealed class RuntimeException(
    string msg) : Exception(msg);
