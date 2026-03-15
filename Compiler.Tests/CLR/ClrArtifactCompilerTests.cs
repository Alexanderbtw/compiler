using System.Diagnostics;

using Compiler.Backend.CLR.Artifacts;
using Compiler.Backend.CLR.Tiering;
using Compiler.Backend.VM;
using Compiler.Frontend.Translation.MIR.Common;
using Compiler.Runtime.VM;

namespace Compiler.Tests.CLR;

public sealed class ClrArtifactCompilerTests
{
    [Fact]
    public void DirectClrAssemblyCompiler_BuildsRunnableArtifact_WithoutVmRuntimeDependency()
    {
        const string source = """
                              fn main() {
                                  var data = array(3, 2);
                                  data[1] = 7;
                                  assert(len(data) == 3);
                                  assert(ord(chr(65)) == 65);
                                  assert('A' == 65);
                                  print(data[0], data[1], len(data), chr(65));
                              }
                              """;

        MirModule mir = TestUtils.BuildMir(source);
        var compiler = new DirectClrAssemblyCompiler();
        string outputDirectory = CreateTempDirectory();

        try
        {
            GeneratedClrArtifact artifact = compiler.Compile(
                mir: mir,
                options: new DirectClrArtifactOptions
                {
                    AssemblyName = "DirectClrArtifact",
                    OutputDirectory = outputDirectory
                });

            ArtifactExecutionResult execution = RunArtifact(artifact.AssemblyPath);

            Assert.Equal(
                expected: 0,
                actual: execution.ExitCode);
            Assert.Equal(
                expected: "2 7 3 A",
                actual: execution.StandardOutput.Trim());
            Assert.True(File.Exists(artifact.DepsFilePath));
            Assert.True(File.Exists(artifact.RuntimeConfigPath));
            Assert.DoesNotContain(
                collection: Directory.EnumerateFiles(
                    Path.GetDirectoryName(artifact.AssemblyPath)!,
                    "Compiler*.dll"),
                filter: path => path.Contains("Compiler.Runtime.VM", StringComparison.Ordinal));
        }
        finally
        {
            Directory.Delete(
                path: outputDirectory,
                recursive: true);
        }
    }

    [Fact]
    public void EmbeddedVmAssemblyCompiler_BuildsRunnableArtifact_WithoutExternalCompilerRuntimeAssemblies()
    {
        const string source = """
                              fn main() {
                                  var a = array(4, 3);
                                  a[1] = 7;
                                  assert(len(a) == 4);
                                  print(a[0], a[1], len(a));
                              }
                              """;

        MirModule mir = TestUtils.BuildMir(source);
        var compiler = new EmbeddedVmAssemblyCompiler();
        string outputDirectory = CreateTempDirectory();

        try
        {
            GeneratedClrArtifact artifact = compiler.Compile(
                mir: mir,
                options: new EmbeddedVmArtifactOptions
                {
                    AssemblyName = "EmbeddedVmArtifact",
                    OutputDirectory = outputDirectory
                });

            ArtifactExecutionResult execution = RunArtifact(artifact.AssemblyPath);

            Assert.Equal(
                expected: 0,
                actual: execution.ExitCode);
            Assert.Equal(
                expected: "3 7 4",
                actual: execution.StandardOutput.Trim());
            Assert.True(File.Exists(artifact.DepsFilePath));
            Assert.True(File.Exists(artifact.RuntimeConfigPath));
            Assert.DoesNotContain(
                collection: Directory.EnumerateFiles(
                    Path.GetDirectoryName(artifact.AssemblyPath)!,
                    "Compiler*.dll"),
                filter: path => path.Contains("Compiler.", StringComparison.Ordinal));
        }
        finally
        {
            Directory.Delete(
                path: outputDirectory,
                recursive: true);
        }
    }

    [Fact]
    public void VmTieredExecutor_PromotesHotFunction_ToClrCompiledTier()
    {
        const string source = """
                              fn fact(n) {
                                  if (n <= 1) {
                                      return 1;
                                  }

                                  return n * fact(n - 1);
                              }

                              fn main() {
                                  fact(5);
                                  fact(6);
                                  return fact(7);
                              }
                              """;

        MirModule mir = TestUtils.BuildMir(source);
        VmCompiledProgram program = new MirBackendCompiler().Compile(mir);
        var vm = new VirtualMachine();
        var executor = new VmTieredExecutor(new VmJitOptions
        {
            FunctionHotThreshold = 2
        });

        VmValue result = executor.Execute(
            program: program,
            vm: vm,
            entryFunctionName: "main");

        FunctionProfile profile = executor.Registry.GetOrAdd("fact");

        Assert.Equal(
            expected: 5040L,
            actual: result.AsInt64());
        Assert.Equal(
            expected: FunctionExecutionTargetKind.ClrCompiled,
            actual: profile.ActiveTargetKind);
        Assert.Equal(
            expected: 1,
            actual: profile.CompilationCount);
        Assert.True(profile.CompiledInvocationCount > 0);
        Assert.True(profile.InvocationCount >= 2);
    }

    [Fact]
    public void DirectClrAssemblyCompiler_Throws_WhenEntryFunctionIsMissing()
    {
        MirModule mir = TestUtils.BuildMir(
            """
            fn main() {
                return 1;
            }
            """);

        var compiler = new DirectClrAssemblyCompiler();
        string outputDirectory = CreateTempDirectory();

        try
        {
            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
            {
                compiler.Compile(
                    mir: mir,
                    options: new DirectClrArtifactOptions
                    {
                        AssemblyName = "MissingEntryArtifact",
                        EntryFunctionName = "missing",
                        OutputDirectory = outputDirectory
                    });
            });

            Assert.Contains(
                expectedSubstring: "entry 'missing' not found",
                actualString: exception.Message,
                comparisonType: StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(
                path: outputDirectory,
                recursive: true);
        }
    }

    [Fact]
    public void RuntimeVmAssembly_DoesNotReference_FrontendTranslationAssembly()
    {
        string[] referencedAssemblies = typeof(VirtualMachine).Assembly
            .GetReferencedAssemblies()
            .Select(reference => reference.Name!)
            .ToArray();

        Assert.DoesNotContain(
            expected: "Compiler.Frontend.Translation",
            collection: referencedAssemblies);
    }

    [Fact]
    public void VmCompiledProgram_DoesNotExpose_SourceMirModule()
    {
        Assert.Null(
            typeof(VmCompiledProgram).GetProperty(
                name: "SourceModule"));
    }

    private static string CreateTempDirectory()
    {
        string path = Path.Combine(
            Path.GetTempPath(),
            "compiler-artifacts-tests",
            Guid.NewGuid()
                .ToString("N"));

        Directory.CreateDirectory(path);
        return path;
    }

    private static ArtifactExecutionResult RunArtifact(
        string assemblyPath)
    {
        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "dotnet",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            }
        };

        process.StartInfo.ArgumentList.Add(assemblyPath);
        process.Start();

        string standardOutput = process.StandardOutput.ReadToEnd();
        string standardError = process.StandardError.ReadToEnd();

        process.WaitForExit();

        return new ArtifactExecutionResult(
            ExitCode: process.ExitCode,
            StandardOutput: standardOutput,
            StandardError: standardError);
    }

    private readonly record struct ArtifactExecutionResult(
        int ExitCode,
        string StandardOutput,
        string StandardError);
}
