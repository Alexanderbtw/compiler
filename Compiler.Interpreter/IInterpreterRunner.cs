using Compiler.Tooling.Options;

namespace Compiler.Interpreter;

public interface IInterpreterRunner
{
    Task<int> RunAsync(
        RunCommandOptions options,
        CancellationToken cancellationToken = default);
}
