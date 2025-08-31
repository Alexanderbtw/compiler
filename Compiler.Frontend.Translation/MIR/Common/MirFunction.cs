using Compiler.Frontend.Translation.MIR.Instructions;
using Compiler.Frontend.Translation.MIR.Operands;

namespace Compiler.Frontend.Translation.MIR.Common;

public sealed class MirFunction
{
    public MirFunction(string name) => Name = name;

    public enum MType
    {
        Obj = 0, // неизвестно/ссылочный (boxed)
        I64 = 1,
        Bool = 2,
        Char = 3
    }

    public List<MirBlock> Blocks { get; } = [];

    public string Name { get; }

    public int NextTempId { get; set; }

    public Dictionary<string, VReg> Parameters { get; } = new();

    public List<string> ParamNames { get; } = new();

    public List<VReg> ParamRegs { get; } = new();

    // Минимальная тип-информация по виртуальным регистрам (заполняет MirTypeAnnotator)
    public Dictionary<int, MType> Types { get; } = new();

    public MirBlock NewBlock(string name)
    {
        var b = new MirBlock(name);
        Blocks.Add(b);
        return b;
    }

    public VReg? NewTemp() => new(++NextTempId);

    public override string ToString() =>
        $"func {Name}\n" + string.Join("\n\n", Blocks.Select(b => b.ToString()));
}
