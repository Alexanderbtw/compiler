using Compiler.Frontend.Translation.MIR.Common;

namespace Compiler.Frontend.Translation.MIR.Optimization;

public interface IMirOptimizationPass
{
    string Name { get; }

    MirPassResult Run(
        MirFunction function,
        MirAnalysisManager analyses);
}
