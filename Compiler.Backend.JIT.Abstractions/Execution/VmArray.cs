namespace Compiler.Backend.JIT.Abstractions.Execution;

/// <summary>
///     Shared array container for execution runtimes.
///     The GC mark bit remains runtime-managed.
/// </summary>
public sealed class VmArray(
    int n) : VmHeapObject
{
    public readonly Value[] Data = new Value[n];

    public int Length => Data.Length;

    public Value this[
        int i]
    {
        get => Data[i];
        set => Data[i] = value;
    }

    internal override void VisitReferences(
        Action<Value> visitor)
    {
        foreach (Value value in Data)
        {
            visitor(value);
        }
    }
}
