namespace Compiler.Backend.VM;

public struct Instr
{
    public int A, B; // small ints: local index, argc, targets
    public int Idx; // indexes into pools (e.g., string pool)
    public long Imm; // immediates for ldc.i64 / char (as int) / bool (0/1)
    public OpCode Op;
    public override string ToString() => $"{Op} A={A} B={B} Imm={Imm} Idx={Idx}";
}

public sealed class VmFunction
{
    public VmFunction(string name, int arity)
    {
        Name = name;
        Arity = arity;
    }

    public int Arity { get; }

    public List<Instr> Code { get; } = [];

    public string Name { get; }

    public int NLocals { get; set; }

    public List<int> ParamLocalIndices { get; } = [];

    public override string ToString() => $"{Name}/{Arity} locals={NLocals} ins={Code.Count}";
}

public sealed class VmModule
{
    private readonly Dictionary<string, int> _fnIdx = new();
    private readonly Dictionary<string, int> _strIdx = new();

    public List<VmFunction> Functions { get; } = [];

    public List<string> StringPool { get; } = [];

    public int AddFunction(VmFunction f)
    {
        int idx = Functions.Count;
        Functions.Add(f);
        _fnIdx[f.Name] = idx;
        return idx;
    }

    public int AddString(string s)
    {
        if (_strIdx.TryGetValue(s, out int id)) return id;
        id = StringPool.Count;
        StringPool.Add(s);
        _strIdx[s] = id;
        return id;
    }
    public bool TryGetFunctionIndex(string name, out int idx) => _fnIdx.TryGetValue(name, out idx);
}
