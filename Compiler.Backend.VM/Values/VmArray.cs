namespace Compiler.Backend.VM.Values;

public sealed class VmArray(
    int n)
{
    public readonly Value[] Data = new Value[n];

    public int Length => Data.Length;

    internal bool GcMarked { get; set; }

    public Value this[
        int i] { get => Data[i]; set => Data[i] = value; }
}
