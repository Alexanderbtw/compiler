using Compiler.Frontend.Translation.MIR.Instructions;
using Compiler.Frontend.Translation.MIR.Operands;

namespace Compiler.Frontend.Translation.MIR.Common;

public sealed class MirFunction
{
    public MirFunction(
        string name)
    {
        Name = name;
    }

    public enum MType
    {
        Obj = 0, // unknown/reference (boxed)
        I64 = 1,
        Bool = 2,
        Char = 3
    }

    public List<MirBlock> Blocks { get; } = [];

    public string Name { get; }

    public int NextTempId { get; set; }

    public Dictionary<string, VReg> Parameters { get; } = new Dictionary<string, VReg>();

    public List<string> ParamNames { get; } = new List<string>();

    public List<VReg> ParamRegs { get; } = new List<VReg>();

    // Minimal type info for virtual registers (filled by MirTypeAnnotator)
    public Dictionary<int, MType> Types { get; } = new Dictionary<int, MType>();

    public MirBlock NewBlock(
        string name)
    {
        var b = new MirBlock(name);
        Blocks.Add(b);

        return b;
    }

    public VReg NewTemp()
    {
        return new VReg(++NextTempId);
    }

    public override string ToString()
    {
        return $"func {Name}\n" + string.Join(
            separator: "\n\n",
            values: Blocks.Select(b => b.ToString()));
    }
}
