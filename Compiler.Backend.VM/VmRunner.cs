using System.Diagnostics;

using Compiler.Backend.JIT.Abstractions;
using Compiler.Core.Builtins;
using Compiler.Frontend;
using Compiler.Frontend.Translation.HIR.Common;
using Compiler.Frontend.Translation.HIR.Semantic.Exceptions;
using Compiler.Frontend.Translation.MIR.Common;
using Compiler.Frontend.Translation.MIR.Optimization.Infrastructure;
using Compiler.Runtime.VM;
using Compiler.Runtime.VM.Execution.GC;
using Compiler.Runtime.VM.Options;
using Compiler.Tooling;
using Compiler.Tooling.Diagnostics;
using Compiler.Tooling.Options;

using Microsoft.Extensions.Logging;

namespace Compiler.Backend.VM;

public sealed class VmRunner(
    IFrontendPipeline pipeline,
    ILogger<VmRunner> logger) : IVmRunner
{
    public Task<int> RunAsync(
        RunCommandOptions options,
        GcCommandOptions gcOptions,
        CancellationToken cancellationToken = default)
    {
        string source;

        try
        {
            source = File.ReadAllText(options.Path);
        }
        catch (Exception ex)
        {
            logger.LogError(
                exception: ex,
                message: "Failed to read '{Path}'",
                options.Path);

            return Task.FromResult(1);
        }

        try
        {
            ProgramHir hir = pipeline.BuildHir(
                src: source,
                verbose: options.Verbose);

            var optimizationOptions = new MirOptimizationOptions(
                enabledPasses: options.EnabledOptimizationPasses,
                collectPassDiagnostics: options.Verbose);

            MirModule mir = pipeline.BuildMir(
                hir: hir,
                options: optimizationOptions);

            if (options.Verbose)
            {
                logger.LogInformation(
                    message: "Optimization level: {OptimizationLevel}",
                    options.OptimizationLevel);

                string enabledPassSummary = string.Join(
                    separator: ", ",
                    values: MirOptimizationPassCatalog.GetEnabledNames(options.EnabledOptimizationPasses));

                logger.LogInformation(
                    message: "Enabled optimization passes: {EnabledPasses}",
                    enabledPassSummary.Length == 0
                        ? "none"
                        : enabledPassSummary);

                if (optimizationOptions.CollectPassDiagnostics && mir.OptimizationReport is not null)
                {
                    string summary = string.Join(
                        separator: ", ",
                        values: mir.OptimizationReport.Passes.Select(pass =>
                            $"{pass.Name}@{pass.Iteration}:{(pass.Changed ? "changed" : "stable")}"));

                    logger.LogInformation(
                        message: "Optimization passes: {Summary}",
                        summary.Length == 0
                            ? "none"
                            : summary);
                }
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

            IBackendCompiler<VmCompiledProgram> compiler = new MirBackendCompiler();

            using Activity? compileActivity = CompilerInstrumentation.ActivitySource.StartActivity("vm.compile");
            var compileWatch = Stopwatch.StartNew();
            VmCompiledProgram program = compiler.Compile(mir);
            compileWatch.Stop();

            CompilerInstrumentation.CompileDurationMs.Record(
                value: compileWatch.Elapsed.TotalMilliseconds,
                tagList: new TagList
                {
                    { "backend", "vm" }
                });

            using Activity? executeActivity = CompilerInstrumentation.ActivitySource.StartActivity("vm.execute");
            using IDisposable? outputOverride = options.Quiet
                ? BuiltinsCore.PushWriter(TextWriter.Null)
                : null;

            var executeWatch = Stopwatch.StartNew();
            VmValue result = program.Execute(
                vm: vm,
                entryFunctionName: "main");

            executeWatch.Stop();

            CompilerInstrumentation.ExecutionDurationMs.Record(
                value: executeWatch.Elapsed.TotalMilliseconds,
                tagList: new TagList
                {
                    { "backend", "vm" }
                });

            if (options.Verbose)
            {
                logger.LogInformation(
                    message: "[ret] {ReturnValue}",
                    vm.FormatValue(result));
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
        catch (Exception ex) when (ex is MiniLangSyntaxException or SemanticException or InvalidOperationException)
        {
            logger.LogError(
                message: "{Message}",
                ex.Message);

            return Task.FromResult(1);
        }
    }
}
