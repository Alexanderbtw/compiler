namespace Compiler.Backend.JIT.Abstractions.Execution;

/// <summary>
///     Shared immutable string container for execution runtimes.
///     The payload stays CLR-backed for now, while lifetime is managed by the runtime GC.
/// </summary>
public sealed class VmString(
    string text) : VmHeapObject
{
    public string Text { get; } = text ?? throw new ArgumentNullException(nameof(text));

    public override string ToString()
    {
        return Text;
    }

    internal override void VisitReferences(
        Action<Value> visitor)
    {
    }
}
