using Compiler.Backend.JIT.Abstractions;
using Compiler.Execution;
using Compiler.Frontend.Translation.MIR.Common;

namespace Compiler.Backend.JIT.CIL;

/// <summary>
///     CIL backend compiler.
/// </summary>
public sealed class MirJitCil : IJit
{
    public ICompiledProgram Compile(
        MirModule mirModule)
    {
        CilEmitter.CilModule cilModule = CilEmitter.EmitModule(mirModule);

        return new CilCompiledProgram(cilModule.Functions);
    }
}
