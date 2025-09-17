namespace Compiler.Backend.VM.Values;

/// <summary>
///     Simple VM array wrapper around a Value[] with a GC mark bit.
/// </summary>
public sealed class VmArray(
    int n)
{
    public readonly Value[] Data = new Value[n];

    public int Length => Data.Length;

    // Mark bit used by the VM's mark–sweep collector
    internal bool GcMarked { get; set; }

    public Value this[
        int i] { get => Data[i]; set => Data[i] = value; }
}
