namespace Compiler.Runtime.VM.Execution;

/// <summary>
///     Heap object discriminant for VM-managed references.
/// </summary>
public enum HeapObjectKind
{
    String,
    Array
}
