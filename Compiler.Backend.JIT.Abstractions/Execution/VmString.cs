namespace Compiler.Backend.JIT.Abstractions.Execution;

/// <summary>
///     Shared immutable string container for execution runtimes.
///     The payload stays CLR-backed for now, while lifetime is managed by the runtime GC.
/// </summary>
public sealed class VmString(
    string text)
{
    public string Text { get; } = text ?? throw new ArgumentNullException(nameof(text));

    internal bool GcMarked { get; set; }

    public override string ToString()
    {
        return Text;
    }
}
