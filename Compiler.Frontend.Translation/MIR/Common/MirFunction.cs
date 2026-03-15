using Compiler.Frontend.Translation.MIR.Instructions;
using Compiler.Frontend.Translation.MIR.Operands;

namespace Compiler.Frontend.Translation.MIR.Common;

/// <summary>
///     function container for MIR
/// </summary>
public sealed class MirFunction(
    string name)
{
    private readonly List<MirBlock> _blocks = [];
    private int _nextTempId;

    public enum MType
    {
        Obj = 0, // unknown/reference (boxed)
        I64 = 1,
        Bool = 2,
        Char = 3
    }

    public IReadOnlyList<MirBlock> Blocks => _blocks;

    public string Name { get; } = name;

    public List<string> ParamNames { get; } = [];

    public List<VReg> ParamRegs { get; } = [];

    // Experimental typing annotations; the main pipeline does not rely on them yet.
    public Dictionary<int, MType> Types { get; } = new Dictionary<int, MType>();

    public MirBlock NewBlock(
        string name)
    {
        var b = new MirBlock(name);
        _blocks.Add(b);

        return b;
    }

    public VReg NewTemp()
    {
        return new VReg(++_nextTempId);
    }

    internal List<MirBlock> MutableBlocks => _blocks;

    public override string ToString()
    {
        return $"func {Name}\n" + string.Join(
            separator: "\n\n",
            values: Blocks.Select(b => b.ToString()));
    }
}
