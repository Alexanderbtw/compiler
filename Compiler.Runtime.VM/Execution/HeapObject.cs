namespace Compiler.Runtime.VM.Execution;

internal abstract class HeapObject(
    HeapObjectKind kind)
{
    public HeapObjectKind Kind { get; } = kind;

    public bool Marked { get; set; }
}

internal sealed class ArrayObject(
    int length) : HeapObject(HeapObjectKind.Array)
{
    public VmValue[] Elements { get; } = new VmValue[length];
}

internal sealed class StringObject(
    string text) : HeapObject(HeapObjectKind.String)
{
    public string Text { get; } = text ?? throw new ArgumentNullException(nameof(text));
}
