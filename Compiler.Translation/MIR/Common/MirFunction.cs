using Compiler.Translation.MIR.Instructions;
using Compiler.Translation.MIR.Operands;

namespace Compiler.Translation.MIR.Common;

public sealed class MirFunction
{
    public MirFunction(string name) => Name = name;

    public List<MirBlock> Blocks { get; } = [];

    public string Name { get; }

    public int NextTempId { get; set; }

    public Dictionary<string, VReg> Parameters { get; } = new();

    public List<string> ParamNames { get; } = new();

    public List<VReg> ParamRegs { get; } = new();

    public MirBlock NewBlock(string name)
    {
        var b = new MirBlock(name);
        Blocks.Add(b);
        return b;
    }

    public VReg NewTemp() => new(++NextTempId);

    public override string ToString() =>
        $"func {Name}\n" + string.Join("\n\n", Blocks.Select(b => b.ToString()));
}
