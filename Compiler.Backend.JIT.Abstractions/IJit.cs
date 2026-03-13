using Compiler.Backend.JIT.Abstractions.Execution;
using Compiler.Frontend.Translation.MIR.Common;

namespace Compiler.Backend.JIT.Abstractions;

/// <summary>
///     Common backend compiler interface shared by execution backends.
/// </summary>
public interface IJit
{
    ICompiledProgram Compile(
        MirModule mirModule);
}
