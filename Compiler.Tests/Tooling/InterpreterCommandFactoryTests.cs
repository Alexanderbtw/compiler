using System.CommandLine;

using Compiler.Interpreter;
using Compiler.Tooling.Options;

using Microsoft.Extensions.Options;

namespace Compiler.Tests.Tooling;

public sealed class InterpreterCommandFactoryTests
{
    [Fact]
    public async Task Run_Binds_Common_Options()
    {
        var runner = new FakeInterpreterRunner();
        var factory = new InterpreterCommandFactory(
            runner: runner,
            defaults: Options.Create(new RunCommandOptions()));

        int exitCode = await factory
            .Create()
            .Parse(
                [
                    "run",
                    "--file",
                    "program.minl",
                    "--verbose",
                    "--quiet",
                    "--time"
                ])
            .InvokeAsync();

        Assert.Equal(
            expected: 0,
            actual: exitCode);

        Assert.NotNull(runner.Options);
        Assert.Equal(
            expected: Path.GetFullPath("program.minl"),
            actual: runner.Options!.Path);

        Assert.True(runner.Options.Verbose);
        Assert.True(runner.Options.Quiet);
        Assert.True(runner.Options.Time);
    }

    private sealed class FakeInterpreterRunner : IInterpreterRunner
    {
        public RunCommandOptions? Options { get; private set; }

        public Task<int> RunAsync(
            RunCommandOptions options,
            CancellationToken cancellationToken = default)
        {
            Options = options;

            return Task.FromResult(0);
        }
    }
}
