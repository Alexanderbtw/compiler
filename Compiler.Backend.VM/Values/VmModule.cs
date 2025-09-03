namespace Compiler.Backend.VM.Values;

public sealed class VmModule
{
    private readonly Dictionary<string, int> _fnIdx = new Dictionary<string, int>();
    private readonly Dictionary<string, int> _strIdx = new Dictionary<string, int>();

    public List<VmFunction> Functions { get; } = [];

    public List<string> StringPool { get; } = [];

    public int AddFunction(
        VmFunction f)
    {
        int idx = Functions.Count;
        Functions.Add(f);
        _fnIdx[f.Name] = idx;

        return idx;
    }

    public int AddString(
        string s)
    {
        if (_strIdx.TryGetValue(
                key: s,
                value: out int id))
        {
            return id;
        }

        id = StringPool.Count;
        StringPool.Add(s);
        _strIdx[s] = id;

        return id;
    }
    public bool TryGetFunctionIndex(
        string name,
        out int idx)
    {
        return _fnIdx.TryGetValue(
            key: name,
            value: out idx);
    }
}
