using System.Diagnostics;

using Compiler.Frontend.Translation.HIR.Common;
using Compiler.Frontend.Translation.HIR.Metadata;
using Compiler.Tooling;
using Compiler.Tooling.Diagnostics;
using Compiler.Tooling.Options;

using Microsoft.Extensions.Logging;

namespace Compiler.Interpreter;

public sealed class InterpreterRunner(
    IFrontendPipeline pipeline,
    ILogger<InterpreterRunner> logger) : IInterpreterRunner
{
    public Task<int> RunAsync(
        RunCommandOptions options,
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

        var interpreter = new Interpreter(hir);

        using Activity? activity = CompilerInstrumentation.ActivitySource.StartActivity("interpreter.execute");
        using IDisposable? outputOverride = options.Quiet
            ? BuiltinsCore.PushWriter(TextWriter.Null)
            : null;

        var sw = Stopwatch.StartNew();
        object? ret = interpreter.Run();
        sw.Stop();

        CompilerInstrumentation.ExecutionDurationMs.Record(
            value: sw.Elapsed.TotalMilliseconds,
            tagList: new TagList
            {
                { "backend", "interpreter" }
            });

        if (options.Verbose)
        {
            logger.LogInformation(
                message: "[ret] {ReturnValue}",
                ret ?? "null");
        }

        if (options.Time)
        {
            logger.LogInformation(
                message: "[time] {ElapsedMs} ms",
                sw.ElapsedMilliseconds);
        }

        return Task.FromResult(0);
    }
}
