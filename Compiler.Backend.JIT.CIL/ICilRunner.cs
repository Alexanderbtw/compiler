using Compiler.Tooling.Options;

namespace Compiler.Backend.JIT.CIL;

public interface ICilRunner
{
    Task<int> RunAsync(
        RunCommandOptions options,
        GcCommandOptions gcOptions,
        CancellationToken cancellationToken = default);
}
