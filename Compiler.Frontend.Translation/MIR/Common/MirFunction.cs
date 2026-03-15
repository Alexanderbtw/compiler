using Compiler.Frontend.Translation.MIR.Instructions;
using Compiler.Frontend.Translation.MIR.Operands;

namespace Compiler.Frontend.Translation.MIR.Common;

/// <summary>
///     function container for MIR
/// </summary>
public sealed class MirFunction(
    string name)
{
    private int _nextTempId;

    public enum MType
    {
        Obj = 0, // unknown/reference (boxed)
        I64 = 1,
        Bool = 2,
        Char = 3
    }

    public IReadOnlyList<MirBlock> Blocks => MutableBlocks;

    public string Name { get; } = name;

    public List<string> ParamNames { get; } = [];

    public List<VReg> ParamRegs { get; } = [];

    // Experimental typing annotations; the main pipeline does not rely on them yet.
    public Dictionary<int, MType> Types { get; } = new Dictionary<int, MType>();

    internal List<MirBlock> MutableBlocks { get; } = [];

    public MirBlock NewBlock(
        string name)
    {
        var b = new MirBlock(name);
        MutableBlocks.Add(b);

        return b;
    }

    public VReg NewTemp()
    {
        return new VReg(++_nextTempId);
    }

    public override string ToString()
    {
        return $"func {Name}\n" + string.Join(
            separator: "\n\n",
            values: Blocks.Select(b => b.ToString()));
    }
}
