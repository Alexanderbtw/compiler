using System.Diagnostics;

using Compiler.Frontend.Translation.MIR.Common;

namespace Compiler.Frontend.Translation.MIR.Optimization.Infrastructure;

public sealed class MirPassManager
{
    public MirOptimizationReport Run(
        MirModule module,
        MirOptimizationOptions options,
        Action<string, bool, double>? passObserver = null)
    {
        var report = new MirOptimizationReport(options);

        IReadOnlyList<MirOptimizationPassRegistration> enabledPasses = MirOptimizationPassCatalog
            .GetEnabledPassesInOrder(options.EnabledPasses)
            .ToArray();

        if (enabledPasses.Count == 0)
        {
            module.OptimizationReport = report;

            return report;
        }

        foreach (MirFunction function in module.Functions)
        {
            var analyses = new MirAnalysisManager(function);

            for (var iteration = 1; iteration <= options.MaxIterations; iteration++)
            {
                var changedInIteration = false;

                foreach (MirOptimizationPassRegistration registration in enabledPasses)
                {
                    changedInIteration |= ExecutePass(
                        registration: registration,
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
        MirOptimizationPassRegistration registration,
        MirFunction function,
        MirAnalysisManager analyses,
        MirOptimizationReport report,
        int iteration,
        Action<string, bool, double>? passObserver)
    {
        IMirOptimizationPass pass = registration.Factory();
        var watch = Stopwatch.StartNew();
        MirPassResult result = pass.Run(
            function: function,
            analyses: analyses);

        watch.Stop();
        analyses.Invalidate(result.InvalidatedAnalyses);

        report.AddPass(
            new MirPassExecution(
                Name: $"{function.Name}:{registration.Name}",
                Changed: result.Changed,
                DurationMs: watch.Elapsed.TotalMilliseconds,
                Iteration: iteration));

        passObserver?.Invoke(
            arg1: registration.Name,
            arg2: result.Changed,
            arg3: watch.Elapsed.TotalMilliseconds);

        return result.Changed;
    }
}
