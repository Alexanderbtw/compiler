using System;

namespace Compiler.Interpreter.Signals;

/// <summary>
///     Internal control-flow signal to unwind frames on 'return'.
///     Carries the returned value.
/// </summary>
internal sealed class ReturnSignal(
    object? v) : Exception
{
    public readonly object? Value = v;
}
