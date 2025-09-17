using System;

namespace Compiler.Interpreter.Signals;

/// <summary>
///     Internal control-flow signal for 'continue' in the interpreter loop.
/// </summary>
internal sealed class ContinueSignal : Exception
{
}
