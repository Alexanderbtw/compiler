using System;

namespace Compiler.Frontend.Interpretation.Exceptions;

public sealed class RuntimeException(string msg) : Exception(msg);
