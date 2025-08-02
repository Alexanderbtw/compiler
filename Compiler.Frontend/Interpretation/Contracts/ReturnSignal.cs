using System;

namespace Compiler.Frontend.Interpretation.Contracts;

internal sealed class ReturnSignal(object? v) : Exception
{
    public readonly object? Value = v;
}