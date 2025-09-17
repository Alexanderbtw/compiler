using System;

namespace Compiler.Interpreter.Signals;

/// <summary>
///     Internal control-flow signal for 'break' in the interpreter loop.
/// </summary>
internal sealed class BreakSignal : Exception;
