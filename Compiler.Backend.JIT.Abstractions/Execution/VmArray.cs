namespace Compiler.Backend.JIT.Abstractions.Execution;

/// <summary>
///     Shared array container for execution runtimes.
///     The GC mark bit remains runtime-managed.
/// </summary>
public sealed class VmArray(
    int n)
{
    public readonly Value[] Data = new Value[n];

    public int Length => Data.Length;

    internal bool GcMarked { get; set; }

    public Value this[
        int i]
    {
        get => Data[i];
        set => Data[i] = value;
    }
}
