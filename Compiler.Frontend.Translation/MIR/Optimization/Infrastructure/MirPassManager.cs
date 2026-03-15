using System.Diagnostics;

using Compiler.Frontend.Translation.MIR.Common;
using Compiler.Frontend.Translation.MIR.Optimization.Passes;

namespace Compiler.Frontend.Translation.MIR.Optimization.Infrastructure;

public sealed class MirPassManager
{
    private readonly IMirOptimizationPass _localCanonicalizationPass = new LocalCanonicalizationPass();

    private readonly IReadOnlyList<IMirOptimizationPass> _loopPasses =
    [
        new GlobalConstantPropagationPass(),
        new BranchFoldingPass(),
        new UnreachableBlockEliminationPass(),
        new DeadCodeEliminationPass(),
        new TrivialBlockCleanupPass()
    ];

    public MirOptimizationReport Run(
        MirModule module,
        MirOptimizationOptions options,
        Action<string, bool, double>? passObserver = null)
    {
        var report = new MirOptimizationReport(options.Level);

        if (options.Level == MirOptimizationLevel.O0)
        {
            module.OptimizationReport = report;

            return report;
        }

        foreach (MirFunction function in module.Functions)
        {
            var analyses = new MirAnalysisManager(function);
            ExecutePass(
                pass: _localCanonicalizationPass,
                function: function,
                analyses: analyses,
                report: report,
                iteration: 0,
                passObserver: passObserver);

            for (var iteration = 1; iteration <= 4; iteration++)
            {
                var changedInIteration = false;

                foreach (IMirOptimizationPass pass in _loopPasses)
                {
                    changedInIteration |= ExecutePass(
                        pass: pass,
                        function: function,
                        analyses: analyses,
                        report: report,
                        iteration: iteration,
                        passObserver: passObserver);
                }

                if (!changedInIteration)
                {
                    break;
                }
            }
        }

        module.OptimizationReport = report;

        return report;
    }

    private static bool ExecutePass(
        IMirOptimizationPass pass,
        MirFunction function,
        MirAnalysisManager analyses,
        MirOptimizationReport report,
        int iteration,
        Action<string, bool, double>? passObserver)
    {
        var watch = Stopwatch.StartNew();
        MirPassResult result = pass.Run(
            function: function,
            analyses: analyses);

        watch.Stop();
        analyses.Invalidate(result.InvalidatedAnalyses);

        report.AddPass(
            new MirPassExecution(
                Name: $"{function.Name}:{pass.Name}",
                Changed: result.Changed,
                DurationMs: watch.Elapsed.TotalMilliseconds,
                Iteration: iteration));

        passObserver?.Invoke(
            arg1: pass.Name,
            arg2: result.Changed,
            arg3: watch.Elapsed.TotalMilliseconds);

        return result.Changed;
    }
}
