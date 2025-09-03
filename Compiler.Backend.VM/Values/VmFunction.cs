namespace Compiler.Backend.VM.Values;

public sealed class VmFunction(
    string name,
    int arity)
{
    public int Arity { get; } = arity;

    public List<Instr> Code { get; } = [];

    public string Name { get; } = name;

    public int NLocals { get; set; }

    public List<int> ParamLocalIndices { get; } = [];

    public override string ToString()
    {
        return $"{Name}/{Arity} locals={NLocals} ins={Code.Count}";
    }
}
