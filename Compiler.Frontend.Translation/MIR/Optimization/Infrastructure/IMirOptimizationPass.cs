using Compiler.Frontend.Translation.MIR.Common;

namespace Compiler.Frontend.Translation.MIR.Optimization.Infrastructure;

public interface IMirOptimizationPass
{
    string Name { get; }

    MirPassResult Run(
        MirFunction function,
        MirAnalysisManager analyses);
}
