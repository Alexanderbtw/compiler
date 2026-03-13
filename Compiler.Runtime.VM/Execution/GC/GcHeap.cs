// uses Value, ValueTag, VmHeapObject, VmArray, VmString

using Compiler.Backend.JIT.Abstractions.Execution;
using Compiler.Runtime.VM.Execution.Diagnostics;

namespace Compiler.Runtime.VM.Execution.GC;

/// <summary>
///     A tiny stop-the-world mark–sweep heap for VM-managed objects.
///     Tracks <see cref="VmArray" /> and <see cref="VmString" /> instances allocated by the VM and
///     reclaims those that are unreachable from the VM roots (operand stack and locals).
///     Objects themselves are ordinary managed instances; removing them from the registry
///     allows the CLR to collect them.
/// </summary>
public sealed class GcHeap(
    int initialThreshold = 1024,
    double growthFactor = 2.0)
{
    private readonly HashSet<VmHeapObject> _allocatedObjects = [];

    // Simple trigger based on number of live objects. Tune or replace with a byte-based threshold if needed.
    private readonly double _growthFactor = Math.Max(
        val1: 1.0,
        val2: growthFactor);

    public int Collections { get; private set; }

    public int CollectionThreshold { get; private set; } = Math.Max(
        val1: 16,
        val2: initialThreshold);

    /// <summary>Total number of arrays currently registered as live.</summary>
    public int LiveArrayCount => _allocatedObjects.Count(static o => o is VmArray);

    /// <summary>Total number of managed objects currently registered as live.</summary>
    public int LiveObjectCount => _allocatedObjects.Count;

    /// <summary>Total number of strings currently registered as live.</summary>
    public int LiveStringCount => _allocatedObjects.Count(static o => o is VmString);

    public int PeakLive { get; private set; }

    public int TotalAllocations { get; private set; }

    /// <summary>Allocate a new VM array and register it with the GC heap.</summary>
    public VmArray AllocateArray(
        int length)
    {
        if (length < 0)
        {
            throw new ArgumentOutOfRangeException(
                paramName: nameof(length),
                message: "array length cannot be negative");
        }

        var array = new VmArray(length);
        _allocatedObjects.Add(array);
        TotalAllocations++;
        VmRuntimeInstrumentation.ArrayAllocations.Add(1);

        UpdatePeakLive();

        return array;
    }

    public VmString AllocateString(
        string value)
    {
        var vmString = new VmString(value);
        _allocatedObjects.Add(vmString);
        TotalAllocations++;
        VmRuntimeInstrumentation.StringAllocations.Add(1);
        UpdatePeakLive();

        return vmString;
    }

    /// <summary>
    ///     Perform a full stop-the-world collection.
    ///     Marks all managed objects reachable from <paramref name="roots" /> and
    ///     sweeps unreachable objects from the registry.
    /// </summary>
    public void Collect(
        IEnumerable<Value> roots)
    {
        Collections++;
        VmRuntimeInstrumentation.Collections.Add(1);

        if (roots is null)
        {
            throw new ArgumentNullException(nameof(roots));
        }

        // MARK
        var markStack = new Stack<VmHeapObject>();

        // Seed from roots
        foreach (Value root in roots)
        {
            MarkValue(
                value: root,
                markStack: markStack);
        }

        // Traverse object graph (currently arrays can contain references; strings cannot)
        while (markStack.Count > 0)
        {
            VmHeapObject current = markStack.Pop();
            current.VisitReferences(value => MarkValue(
                value: value,
                markStack: markStack));
        }

        // SWEEP
        var unreachable = new List<VmHeapObject>();

        foreach (VmHeapObject heapObject in _allocatedObjects)
        {
            if (!heapObject.GcMarked)
            {
                unreachable.Add(heapObject);
            }
            else
            {
                heapObject.GcMarked = false;
            }
        }

        foreach (VmHeapObject dead in unreachable)
        {
            _allocatedObjects.Remove(dead);
        }

        UpdatePeakLive();

        // if still near the threshold after collection, grow it to amortize cost
        if (LiveObjectCount >= CollectionThreshold)
        {
            var grown = (int)Math.Ceiling(CollectionThreshold * _growthFactor);
            CollectionThreshold = Math.Max(
                val1: grown,
                val2: LiveObjectCount + 1);
        }
    }

    public GcStats GetStats()
    {
        return new GcStats(
            totalAllocations: TotalAllocations,
            collections: Collections,
            peakLive: PeakLive,
            live: LiveObjectCount,
            threshold: CollectionThreshold,
            growthFactor: _growthFactor);
    }

    /// <summary>Returns true if the heap suggests doing a collection after an allocation.</summary>
    public bool ShouldCollect()
    {
        return LiveObjectCount >= CollectionThreshold;
    }

    private void MarkHeapObject(
        VmHeapObject heapObject,
        Stack<VmHeapObject> markStack)
    {
        if (_allocatedObjects.Contains(heapObject) && !heapObject.GcMarked)
        {
            heapObject.GcMarked = true;
            markStack.Push(heapObject);
        }
    }

    private void MarkValue(
        Value value,
        Stack<VmHeapObject> markStack)
    {
        switch (value.Tag)
        {
            case ValueTag.Array:
                MarkHeapObject(
                    heapObject: value.AsArray(),
                    markStack: markStack);
                break;
            case ValueTag.String:
                MarkHeapObject(
                    heapObject: value.AsString(),
                    markStack: markStack);
                break;
        }
    }

    private void UpdatePeakLive()
    {
        if (LiveObjectCount > PeakLive)
        {
            PeakLive = LiveObjectCount;
        }
    }
}
