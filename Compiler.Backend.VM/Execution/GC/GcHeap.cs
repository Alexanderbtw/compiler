// uses Value, ValueTag, VmArray

using Compiler.Backend.VM.Values;

namespace Compiler.Backend.VM.Execution.GC;

/// <summary>
///     A tiny stop-the-world mark–sweep heap for VM-managed objects.
///     Currently, tracks <see cref="VmArray" /> instances allocated by the VM and
///     reclaims those that are unreachable from the VM roots (operand stack and locals).
///     Objects themselves are ordinary managed instances; removing them from the registry
///     allows the CLR to collect them.
/// </summary>
public sealed class GcHeap(
    int initialThreshold = 1024,
    double growthFactor = 2.0)
{
    private readonly HashSet<VmArray> _allocatedArrays = [];

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

        if (_allocatedArrays.Count > PeakLive)
        {
            PeakLive = _allocatedArrays.Count;
        }

        return array;
    }

    /// <summary>
    ///     Perform a full stop-the-world collection.
    ///     Marks all arrays reachable from <paramref name="roots" /> and
    ///     sweeps unreachable arrays from the registry.
    /// </summary>
    public void Collect(
        IEnumerable<Value> roots)
    {
        Collections++;

        if (roots is null)
        {
            throw new ArgumentNullException(nameof(roots));
        }

        // MARK
        var markStack = new Stack<VmArray>();

        // Seed from roots
        foreach (Value root in roots)
        {
            if (root.Tag == ValueTag.Array)
            {
                VmArray array = root.AsArray();

                if (_allocatedArrays.Contains(array) && !array.GcMarked)
                {
                    array.GcMarked = true;
                    markStack.Push(array);
                }
            }
        }

        // Traverse array graph (handles nested arrays)
        while (markStack.Count > 0)
        {
            VmArray array = markStack.Pop();
            int length = array.Length;

            for (int index = 0; index < length; index++)
            {
                Value element = array[index];

                if (element.Tag == ValueTag.Array)
                {
                    VmArray child = element.AsArray();

                    if (_allocatedArrays.Contains(child) && !child.GcMarked)
                    {
                        child.GcMarked = true;
                        markStack.Push(child);
                    }
                }
            }
        }

        // SWEEP
        var unreachable = new List<VmArray>();

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

        // Remove unreachable arrays from the registry (CLR will reclaim them later)
        foreach (VmArray dead in unreachable)
        {
            _allocatedArrays.Remove(dead);
        }

        if (_allocatedArrays.Count > PeakLive)
        {
            PeakLive = _allocatedArrays.Count;
        }

        // Heuristic: if still near the threshold after collection, grow it to amortize cost
        if (_allocatedArrays.Count >= CollectionThreshold)
        {
            int grown = (int)Math.Ceiling(CollectionThreshold * _growthFactor);
            CollectionThreshold = Math.Max(
                val1: grown,
                val2: _allocatedArrays.Count + 1);
        }
    }

    public GcStats GetStats()
    {
        return new GcStats(
            totalAllocations: TotalAllocations,
            collections: Collections,
            peakLive: PeakLive,
            live: _allocatedArrays.Count,
            threshold: CollectionThreshold,
            growthFactor: _growthFactor);
    }

    /// <summary>Configure the collection threshold (minimum 16).</summary>
    public void SetThreshold(
        int threshold)
    {
        CollectionThreshold = Math.Max(
            val1: 16,
            val2: threshold);
    }

    /// <summary>Returns true if the heap suggests doing a collection after an allocation.</summary>
    public bool ShouldCollect()
    {
        return _allocatedArrays.Count >= CollectionThreshold;
    }
}
