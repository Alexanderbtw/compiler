namespace Compiler.Runtime.VM.Execution;

/// <summary>
///     Runtime services required by compiled VM-compatible execution tiers.
/// </summary>
public interface IVmExecutionRuntime
{
    /// <summary>
    ///     Allocates a VM array value.
    /// </summary>
    /// <param name="length">Requested array length.</param>
    /// <returns>A reference value pointing at the allocated array.</returns>
    VmValue AllocateArray(
        int length);

    /// <summary>
    ///     Allocates a VM string value.
    /// </summary>
    /// <param name="value">Source text.</param>
    /// <returns>A reference value pointing at the allocated string.</returns>
    VmValue AllocateString(
        string value);

    /// <summary>
    ///     Enters a compiled frame and registers its roots for the GC.
    /// </summary>
    /// <param name="locals">Locals/registers owned by the compiled frame.</param>
    /// <param name="constants">Materialized constants owned by the compiled frame.</param>
    void EnterCompiledFrame(
        VmValue[] locals,
        VmValue[] constants);

    /// <summary>
    ///     Leaves the current compiled frame and removes its GC roots.
    /// </summary>
    void ExitCompiledFrame();

    /// <summary>
    ///     Formats a value for diagnostics and builtins.
    /// </summary>
    /// <param name="value">Value to format.</param>
    /// <returns>Display representation.</returns>
    string FormatValue(
        VmValue value);

    /// <summary>
    ///     Loads an array element.
    /// </summary>
    /// <param name="handle">Array handle.</param>
    /// <param name="index">Zero-based index.</param>
    /// <returns>The element at <paramref name="index" />.</returns>
    VmValue GetArrayElement(
        int handle,
        int index);

    /// <summary>
    ///     Gets the length of an array.
    /// </summary>
    /// <param name="handle">Array handle.</param>
    /// <returns>Array length.</returns>
    int GetArrayLength(
        int handle);

    /// <summary>
    ///     Returns the heap object kind for a handle.
    /// </summary>
    /// <param name="handle">Heap handle.</param>
    /// <returns>The kind of heap object referenced by <paramref name="handle" />.</returns>
    HeapObjectKind GetHeapObjectKind(
        int handle);

    /// <summary>
    ///     Reads a string payload.
    /// </summary>
    /// <param name="handle">String handle.</param>
    /// <returns>Stored string text.</returns>
    string GetString(
        int handle);

    /// <summary>
    ///     Stores an element in an array.
    /// </summary>
    /// <param name="handle">Array handle.</param>
    /// <param name="index">Zero-based index.</param>
    /// <param name="value">Value to store.</param>
    void SetArrayElement(
        int handle,
        int index,
        VmValue value);
}
