namespace Compiler.Tooling.Options;

public sealed class RunCommandOptions
{
    public string Path { get; set; } = "main.minl";

    public bool Quiet { get; set; }

    public bool Time { get; set; }

    public bool Verbose { get; set; }
}
