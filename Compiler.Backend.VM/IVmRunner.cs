using Compiler.Tooling.Options;

namespace Compiler.Backend.VM;

public interface IVmRunner
{
    Task<int> RunAsync(
        RunCommandOptions options,
        GcCommandOptions gcOptions,
        CancellationToken cancellationToken = default);
}
