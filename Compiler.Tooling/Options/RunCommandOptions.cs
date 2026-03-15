using Compiler.Frontend.Translation.MIR.Common;

namespace Compiler.Tooling.Options;

public sealed class RunCommandOptions
{
    private MirOptimizationPasses _enabledOptimizationPasses = MirOptimizationPasses.StableDefault;

    private MirOptimizationLevel _optimizationLevel = MirOptimizationLevel.O1;

    public MirOptimizationPasses EnabledOptimizationPasses
    {
        get => _enabledOptimizationPasses;
        set
        {
            _enabledOptimizationPasses = value;
            _optimizationLevel = value == MirOptimizationPasses.None
                ? MirOptimizationLevel.O0
                : MirOptimizationLevel.O1;
        }
    }

    public MirOptimizationLevel OptimizationLevel
    {
        get => _optimizationLevel;
        set
        {
            _optimizationLevel = value;
            _enabledOptimizationPasses = value == MirOptimizationLevel.O0
                ? MirOptimizationPasses.None
                : MirOptimizationPasses.StableDefault;
        }
    }

    public string Path { get; set; } = "main.minl";

    public bool Quiet { get; set; }

    public bool Time { get; set; }

    public bool Verbose { get; set; }
}
