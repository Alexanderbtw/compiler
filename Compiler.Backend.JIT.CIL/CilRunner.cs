using System.Diagnostics;

using Compiler.Execution;
using Compiler.Frontend.Translation.HIR.Common;
using Compiler.Frontend.Translation.HIR.Metadata;
using Compiler.Frontend.Translation.MIR.Common;
using Compiler.Runtime.VM;
using Compiler.Runtime.VM.Execution.GC;
using Compiler.Runtime.VM.Options;
using Compiler.Tooling;
using Compiler.Tooling.Diagnostics;
using Compiler.Tooling.Options;

using Microsoft.Extensions.Logging;

namespace Compiler.Backend.JIT.CIL;

public sealed class CilRunner(
    IFrontendPipeline pipeline,
    ILogger<CilRunner> logger) : ICilRunner
{
    public Task<int> RunAsync(
        RunCommandOptions options,
        GcCommandOptions gcOptions,
        CancellationToken cancellationToken = default)
    {
        string src;

        try
        {
            src = File.ReadAllText(options.Path);
        }
        catch (Exception ex)
        {
            logger.LogError(
                exception: ex,
                message: "Failed to read '{Path}'",
                options.Path);

            return Task.FromResult(1);
        }

        ProgramHir hir = pipeline.BuildHir(
            src: src,
            verbose: options.Verbose);

        MirModule mir = pipeline.BuildMir(hir);

        if (options.Verbose)
        {
            logger.LogInformation(
                message: "MIR:{NewLine}{Mir}",
                Environment.NewLine,
                mir);
        }

        var vm = new VirtualMachine(
            options: new GcOptions
            {
                AutoCollect = gcOptions.AutoCollect,
                InitialThreshold = gcOptions.InitialThreshold,
                GrowthFactor = gcOptions.GrowthFactor
            });

        var jit = new MirJitCil();

        using Activity? compileActivity = CompilerInstrumentation.ActivitySource.StartActivity("cil.compile");
        var compileWatch = Stopwatch.StartNew();
        ICompiledProgram program = jit.Compile(mir);
        compileWatch.Stop();

        CompilerInstrumentation.CompileDurationMs.Record(
            value: compileWatch.Elapsed.TotalMilliseconds,
            tagList: new TagList
            {
                { "backend", "cil" }
            });

        using Activity? executeActivity = CompilerInstrumentation.ActivitySource.StartActivity("cil.execute");
        using IDisposable? outputOverride = options.Quiet
            ? BuiltinsCore.PushWriter(TextWriter.Null)
            : null;

        var executeWatch = Stopwatch.StartNew();
        Value ret = program.Execute(
            runtime: vm,
            entryFunctionName: "main");

        executeWatch.Stop();

        CompilerInstrumentation.ExecutionDurationMs.Record(
            value: executeWatch.Elapsed.TotalMilliseconds,
            tagList: new TagList
            {
                { "backend", "cil" }
            });

        if (options.Verbose)
        {
            logger.LogInformation(
                message: "[ret] {ReturnValue}",
                ret);
        }

        if (options.Time)
        {
            logger.LogInformation(
                message: "[time] {ElapsedMs} ms",
                executeWatch.ElapsedMilliseconds);
        }

        if (gcOptions.PrintStats)
        {
            GcStats stats = vm.GetGcStats();
            logger.LogInformation(
                message: "[gc] mode=vm auto={Auto} threshold={Threshold} growth={Growth}",
                gcOptions.AutoCollect
                    ? "on"
                    : "off",
                stats.Threshold,
                stats.GrowthFactor);

            logger.LogInformation(
                message: "[gc] allocations={Allocations} collections={Collections} live={Live} peak_live={PeakLive}",
                stats.TotalAllocations,
                stats.Collections,
                stats.Live,
                stats.PeakLive);
        }

        return Task.FromResult(0);
    }
}
