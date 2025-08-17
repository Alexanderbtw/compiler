using System;

namespace Compiler.Interpreter.Signals;

internal sealed class ReturnSignal(object? v) : Exception
{
    public readonly object? Value = v;
}