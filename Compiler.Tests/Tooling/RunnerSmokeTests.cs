using Compiler.Backend.JIT.CIL;
using Compiler.Interpreter;
using Compiler.Tooling;
using Compiler.Tooling.Options;

using Microsoft.Extensions.Logging.Abstractions;

namespace Compiler.Tests.Tooling;

public sealed class RunnerSmokeTests
{
    [Fact]
    public async Task CilRunner_Executes_Source_File()
    {
        string path = CreateProgramFile();

        try
        {
            var pipeline = new FrontendPipeline(NullLogger<FrontendPipeline>.Instance);
            var runner = new CilRunner(
                pipeline: pipeline,
                logger: NullLogger<CilRunner>.Instance);

            int exitCode = await runner.RunAsync(
                options: new RunCommandOptions
                {
                    Path = path,
                    Quiet = true,
                    Time = true,
                    Verbose = true
                },
                gcOptions: new GcCommandOptions
                {
                    AutoCollect = true,
                    GrowthFactor = 1.5,
                    InitialThreshold = 64,
                    PrintStats = true
                });

            Assert.Equal(
                expected: 0,
                actual: exitCode);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task InterpreterRunner_Executes_Source_File()
    {
        string path = CreateProgramFile();

        try
        {
            var pipeline = new FrontendPipeline(NullLogger<FrontendPipeline>.Instance);
            var runner = new InterpreterRunner(
                pipeline: pipeline,
                logger: NullLogger<InterpreterRunner>.Instance);

            int exitCode = await runner.RunAsync(
                options: new RunCommandOptions
                {
                    Path = path,
                    Quiet = true,
                    Time = true,
                    Verbose = true
                });

            Assert.Equal(
                expected: 0,
                actual: exitCode);
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static string CreateProgramFile()
    {
        string path = Path.Combine(
            path1: Path.GetTempPath(),
            path2: $"{Guid.NewGuid():N}.minl");

        File.WriteAllText(
            path: path,
            contents: ProgramSource);

        return path;
    }

    private const string ProgramSource = """
                                         fn main() {
                                             print(1);
                                             return 2;
                                         }
                                         """;
}
