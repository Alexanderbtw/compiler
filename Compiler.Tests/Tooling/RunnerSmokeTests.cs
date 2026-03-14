using Compiler.Backend.VM;
using Compiler.Frontend;
using Compiler.Interpreter;
using Compiler.Tooling;
using Compiler.Tooling.Options;

using Microsoft.Extensions.Logging.Abstractions;

namespace Compiler.Tests.Tooling;

public sealed class RunnerSmokeTests
{
    [Fact]
    public async Task FrontendPipeline_Invalid_Syntax_Throws_Controlled_Exception()
    {
        var pipeline = new FrontendPipeline(NullLogger<FrontendPipeline>.Instance);

        var ex = Assert.Throws<MiniLangSyntaxException>(() => pipeline.BuildHir("fn main( {"));
        Assert.Contains(
            expectedSubstring: "line 1",
            actualString: ex.Message,
            comparisonType: StringComparison.Ordinal);
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

    [Fact]
    public async Task InterpreterRunner_Returns_Error_Code_For_Syntax_Errors()
    {
        string path = CreateProgramFile("fn main( {");

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
                    Quiet = true
                });

            Assert.Equal(
                expected: 1,
                actual: exitCode);
        }
        finally
        {
            File.Delete(path);
        }
    }
    [Fact]
    public async Task VmRunner_Executes_Source_File()
    {
        string path = CreateProgramFile();

        try
        {
            var pipeline = new FrontendPipeline(NullLogger<FrontendPipeline>.Instance);
            var runner = new VmRunner(
                pipeline: pipeline,
                logger: NullLogger<VmRunner>.Instance);

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
    public async Task VmRunner_Returns_Error_Code_For_Syntax_Errors()
    {
        string path = CreateProgramFile("fn main( {");

        try
        {
            var pipeline = new FrontendPipeline(NullLogger<FrontendPipeline>.Instance);
            var runner = new VmRunner(
                pipeline: pipeline,
                logger: NullLogger<VmRunner>.Instance);

            int exitCode = await runner.RunAsync(
                options: new RunCommandOptions
                {
                    Path = path,
                    Quiet = true
                },
                gcOptions: new GcCommandOptions());

            Assert.Equal(
                expected: 1,
                actual: exitCode);
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static string CreateProgramFile()
    {
        return CreateProgramFile(ProgramSource);
    }

    private static string CreateProgramFile(
        string source)
    {
        string path = Path.Combine(
            path1: Path.GetTempPath(),
            path2: $"{Guid.NewGuid():N}.minl");

        File.WriteAllText(
            path: path,
            contents: source);

        return path;
    }

    private const string ProgramSource = """
                                         fn main() {
                                             print(1);
                                             return 2;
                                         }
                                         """;
}
