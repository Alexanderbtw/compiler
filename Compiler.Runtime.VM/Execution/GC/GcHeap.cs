// uses Value, ValueTag, VmArray, VmString

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
    private readonly HashSet<VmArray> _allocatedArrays = [];
    private readonly HashSet<VmString> _allocatedStrings = [];

    // Simple trigger based on number of live objects. Tune or replace with a byte-based threshold if needed.
    private readonly double _growthFactor = Math.Max(
        val1: 1.0,
        val2: growthFactor);

    public int Collections { get; private set; }

    public int CollectionThreshold { get; private set; } = Math.Max(
        val1: 16,
        val2: initialThreshold);

    /// <summary>Total number of arrays currently registered as live.</summary>
    public int LiveArrayCount => _allocatedArrays.Count;

    /// <summary>Total number of managed objects currently registered as live.</summary>
    public int LiveObjectCount => _allocatedArrays.Count + _allocatedStrings.Count;

    /// <summary>Total number of strings currently registered as live.</summary>
    public int LiveStringCount => _allocatedStrings.Count;

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
        _allocatedArrays.Add(array);
        TotalAllocations++;
        VmRuntimeInstrumentation.ArrayAllocations.Add(1);

        UpdatePeakLive();

        return array;
    }

    public VmString AllocateString(
        string value)
    {
        var vmString = new VmString(value);
        _allocatedStrings.Add(vmString);
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
        var markStack = new Stack<VmArray>();

        // Seed from roots
        foreach (Value root in roots)
        {
            switch (root.Tag)
            {
                case ValueTag.Array:
                    MarkArray(
                        array: root.AsArray(),
                        markStack: markStack);

                    break;
                case ValueTag.String:
                    MarkString(root.AsString());

                    break;
            }
        }

        // Traverse array graph (handles nested arrays)
        while (markStack.Count > 0)
        {
            VmArray array = markStack.Pop();
            int length = array.Length;

            for (var index = 0; index < length; index++)
            {
                Value element = array[index];

                switch (element.Tag)
                {
                    case ValueTag.Array:
                        MarkArray(
                            array: element.AsArray(),
                            markStack: markStack);

                        break;
                    case ValueTag.String:
                        MarkString(element.AsString());

                        break;
                }
            }
        }

        // SWEEP
        var unreachable = new List<VmArray>();
        var unreachableStrings = new List<VmString>();

        foreach (VmArray array in _allocatedArrays)
        {
            if (!array.GcMarked)
            {
                unreachable.Add(array);
            }
            else
            {
                array.GcMarked = false; // unmark survivor for next cycle
            }
        }

        foreach (VmString vmString in _allocatedStrings)
        {
            if (!vmString.GcMarked)
            {
                unreachableStrings.Add(vmString);
            }
            else
            {
                vmString.GcMarked = false;
            }
        }

        // Remove unreachable arrays from the registry (CLR will reclaim them later)
        foreach (VmArray dead in unreachable)
        {
            _allocatedArrays.Remove(dead);
        }

        foreach (VmString dead in unreachableStrings)
        {
            _allocatedStrings.Remove(dead);
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

    private void MarkArray(
        VmArray array,
        Stack<VmArray> markStack)
    {
        if (_allocatedArrays.Contains(array) && !array.GcMarked)
        {
            array.GcMarked = true;
            markStack.Push(array);
        }
    }

    private void MarkString(
        VmString vmString)
    {
        if (_allocatedStrings.Contains(vmString))
        {
            vmString.GcMarked = true;
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
