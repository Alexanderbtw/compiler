using Compiler.Frontend.Translation.MIR.Common;

namespace Compiler.Tooling.Options;

public sealed class RunCommandOptions
{
    public MirOptimizationLevel OptimizationLevel { get; set; } = MirOptimizationLevel.O1;

    public string Path { get; set; } = "main.minl";

    public bool Quiet { get; set; }

    public bool Time { get; set; }

    public bool Verbose { get; set; }
}
