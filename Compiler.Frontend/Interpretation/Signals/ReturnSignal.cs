using System;

namespace Compiler.Frontend.Interpretation.Signals;

internal sealed class ReturnSignal(object? v) : Exception
{
    public readonly object? Value = v;
}