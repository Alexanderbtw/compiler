using Compiler.Frontend.Translation.MIR.Common;

namespace Compiler.Backend.JIT.Abstractions;

/// <summary>
///     Compiles MIR into an executable backend artifact.
/// </summary>
public interface IBackendCompiler<out TCompiledProgram>
{
    TCompiledProgram Compile(
        MirModule mirModule);
}
