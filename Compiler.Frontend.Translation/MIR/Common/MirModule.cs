namespace Compiler.Frontend.Translation.MIR.Common;

public sealed class MirModule
{
    public List<MirFunction> Functions { get; } = [];

    public override string ToString()
    {
        return string.Join(
            separator: "\n\n",
            values: Functions.Select(f => f.ToString()));
    }
}
