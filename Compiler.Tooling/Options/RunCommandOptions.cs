using Compiler.Frontend.Translation.MIR.Common;

namespace Compiler.Tooling.Options;

public sealed class RunCommandOptions
{
    public MirOptimizationPasses EnabledOptimizationPasses { get; set; } = MirOptimizationPasses.StableDefault;

    public string Path { get; set; } = "main.minl";

    public bool Quiet { get; set; }

    public bool Time { get; set; }

    public bool Verbose { get; set; }
}
