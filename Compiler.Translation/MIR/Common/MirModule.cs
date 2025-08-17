namespace Compiler.Translation.MIR.Common;

public sealed class MirModule
{
    public List<MirFunction> Functions { get; } = [];

    public override string ToString() => string.Join("\n\n", Functions.Select(f => f.ToString()));
}