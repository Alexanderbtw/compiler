namespace Compiler.Backend.JIT.Abstractions.Execution;

/// <summary>
///     Base type for GC-tracked runtime objects.
/// </summary>
public abstract class VmHeapObject
{
    internal bool GcMarked { get; set; }

    internal abstract void VisitReferences(
        Action<Value> visitor);
}
