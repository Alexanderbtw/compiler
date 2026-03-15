using BenchmarkDotNet.Attributes;

using Compiler.Backend.JIT.Abstractions;
using Compiler.Backend.VM;
using Compiler.Core.Builtins;
using Compiler.Frontend.Translation.HIR.Common;
using Compiler.Frontend.Translation.MIR.Common;
using Compiler.Runtime.VM;
using Compiler.Tooling;

using Microsoft.Extensions.Logging;

namespace Compiler.Benchmarks;

[MemoryDiagnoser]
public class MiniLangBenchmarks
{
    private VmCompiledProgram? factorialProgram;
    private string factorialSource = string.Empty;
    private ILoggerFactory? loggerFactory;
    private IFrontendPipeline? pipeline;
    private VmCompiledProgram? sieveProgram;
    private string sieveSource = string.Empty;
    private VmCompiledProgram? sortingProgram;
    private string sortingSource = string.Empty;

    [Benchmark]
    public VmCompiledProgram Compile_ArraySorting()
    {
        return CompileProgram(sortingSource);
    }

    [Benchmark]
    public VmCompiledProgram Compile_Factorial()
    {
        return CompileProgram(factorialSource);
    }

    [Benchmark]
    public VmCompiledProgram Compile_PrimeSieve()
    {
        return CompileProgram(sieveSource);
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

        factorialProgram = CompileProgram(factorialSource);
        sortingProgram = CompileProgram(sortingSource);
        sieveProgram = CompileProgram(sieveSource);
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

    [Benchmark]
    public object? VM_ArraySorting()
    {
        return ExecuteCompiledProgram(sortingProgram!);
    }

    [Benchmark]
    public object? VM_Factorial()
    {
        return ExecuteCompiledProgram(factorialProgram!);
    }

    [Benchmark]
    public object? VM_PrimeSieve()
    {
        return ExecuteCompiledProgram(sieveProgram!);
    }

    private VmCompiledProgram CompileProgram(
        string source)
    {
        ProgramHir hir = pipeline!.BuildHir(
            src: source,
            verbose: false);

        MirModule mir = pipeline.BuildMir(
            hir: hir,
            options: new MirOptimizationOptions(MirOptimizationLevel.O1));

        IBackendCompiler<VmCompiledProgram> compiler = new MirBackendCompiler();

        return compiler.Compile(mir);
    }

    private object? ExecuteCompiledProgram(
        VmCompiledProgram program)
    {
        var vm = new VirtualMachine();

        using IDisposable outputOverride = BuiltinsCore.PushWriter(TextWriter.Null);

        VmValue value = program.Execute(
            vm: vm,
            entryFunctionName: "main");

        return vm.ExportValue(value);
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
