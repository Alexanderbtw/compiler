using BenchmarkDotNet.Attributes;

using Compiler.Backend.JIT.CIL;
using Compiler.Execution;
using Compiler.Frontend.Translation.HIR.Common;
using Compiler.Frontend.Translation.HIR.Metadata;
using Compiler.Frontend.Translation.MIR.Common;
using Compiler.Runtime.VM;
using Compiler.Tooling;

using Microsoft.Extensions.Logging;

namespace Compiler.Benchmarks;

[MemoryDiagnoser]
public class MiniLangBenchmarks
{
    private string factorialSource = string.Empty;
    private ILoggerFactory? loggerFactory;
    private IFrontendPipeline? pipeline;
    private string sieveSource = string.Empty;
    private string sortingSource = string.Empty;

    [Benchmark]
    public Value CIL_ArraySorting()
    {
        return RunCil(sortingSource);
    }

    [Benchmark]
    public Value CIL_Factorial()
    {
        return RunCil(factorialSource);
    }

    [Benchmark]
    public Value CIL_PrimeSieve()
    {
        return RunCil(sieveSource);
    }

    [GlobalCleanup]
    public void GlobalCleanup()
    {
        loggerFactory?.Dispose();
    }

    [GlobalSetup]
    public void GlobalSetup()
    {
        loggerFactory = LoggerFactory.Create(static builder => builder.SetMinimumLevel(LogLevel.None));
        pipeline = new FrontendPipeline(loggerFactory.CreateLogger<FrontendPipeline>());

        string tasksDirectory = Path.Combine(
            path1: AppContext.BaseDirectory,
            path2: "Tasks");

        factorialSource = File.ReadAllText(
            Path.Combine(
                path1: tasksDirectory,
                path2: "factorial_calculation.minl"));

        sortingSource = File.ReadAllText(
            Path.Combine(
                path1: tasksDirectory,
                path2: "array_sorting.minl"));

        sieveSource = File.ReadAllText(
            Path.Combine(
                path1: tasksDirectory,
                path2: "prime_number_generation.minl"));
    }

    [Benchmark]
    public object? Interpreter_ArraySorting()
    {
        return RunInterpreter(sortingSource);
    }

    [Benchmark(Baseline = true)]
    public object? Interpreter_Factorial()
    {
        return RunInterpreter(factorialSource);
    }

    [Benchmark]
    public object? Interpreter_PrimeSieve()
    {
        return RunInterpreter(sieveSource);
    }

    private Value RunCil(
        string source)
    {
        ProgramHir hir = pipeline!.BuildHir(
            src: source,
            verbose: false);

        MirModule mir = pipeline.BuildMir(hir);

        var vm = new VirtualMachine();
        var jit = new MirJitCil();
        ICompiledProgram program = jit.Compile(mir);

        using IDisposable outputOverride = BuiltinsCore.PushWriter(TextWriter.Null);

        return program.Execute(
            runtime: vm,
            entryFunctionName: "main");
    }

    private object? RunInterpreter(
        string source)
    {
        ProgramHir hir = pipeline!.BuildHir(
            src: source,
            verbose: false);

        var interpreter = new Interpreter.Interpreter(hir);

        using IDisposable outputOverride = BuiltinsCore.PushWriter(TextWriter.Null);

        return interpreter.Run();
    }
}
