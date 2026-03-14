using Compiler.Runtime.VM.Execution.Diagnostics;

namespace Compiler.Runtime.VM.Execution.GC;

/// <summary>
///     Tiny stop-the-world mark-sweep heap backed by handle-addressed objects.
/// </summary>
public sealed class GcHeap(
    int initialThreshold = 1024,
    double growthFactor = 2.0)
{
    private readonly Stack<int> _freeHandles = [];

    private readonly double _growthFactor = Math.Max(
        val1: 1.0,
        val2: growthFactor);

    private readonly List<HeapObject?> _objects = [];

    public int Collections { get; private set; }

    public int CollectionThreshold { get; private set; } = Math.Max(
        val1: 16,
        val2: initialThreshold);

    public int LiveArrayCount => _objects.Count(static objectSlot => objectSlot is ArrayObject);

    public int LiveObjectCount => _objects.Count - _freeHandles.Count;

    public int LiveStringCount => _objects.Count(static objectSlot => objectSlot is StringObject);

    public int PeakLive { get; private set; }

    public int TotalAllocations { get; private set; }

    public int AllocateArray(
        int length)
    {
        if (length < 0)
        {
            throw new ArgumentOutOfRangeException(
                paramName: nameof(length),
                message: "array length cannot be negative");
        }

        int handle = AllocateObject(new ArrayObject(length));
        VmRuntimeInstrumentation.ArrayAllocations.Add(1);

        return handle;
    }

    public int AllocateString(
        string value)
    {
        int handle = AllocateObject(new StringObject(value));
        VmRuntimeInstrumentation.StringAllocations.Add(1);

        return handle;
    }

    public void Collect(
        IEnumerable<VmValue> roots)
    {
        ArgumentNullException.ThrowIfNull(roots);

        Collections++;
        VmRuntimeInstrumentation.Collections.Add(1);

        var markStack = new Stack<int>();

        foreach (VmValue root in roots)
        {
            MarkValue(
                value: root,
                markStack: markStack);
        }

        while (markStack.Count > 0)
        {
            HeapObject heapObject = GetRequiredObject(markStack.Pop());

            if (heapObject is not ArrayObject arrayObject)
            {
                continue;
            }

            foreach (VmValue element in arrayObject.Elements)
            {
                MarkValue(
                    value: element,
                    markStack: markStack);
            }
        }

        for (var index = 0; index < _objects.Count; index++)
        {
            HeapObject? heapObject = _objects[index];

            if (heapObject is null)
            {
                continue;
            }

            if (!heapObject.Marked)
            {
                _objects[index] = null;
                _freeHandles.Push(index + 1);

                continue;
            }

            heapObject.Marked = false;
        }

        UpdatePeakLive();

        if (LiveObjectCount >= CollectionThreshold)
        {
            var grown = (int)Math.Ceiling(CollectionThreshold * _growthFactor);
            CollectionThreshold = Math.Max(
                val1: grown,
                val2: LiveObjectCount + 1);
        }
    }

    public VmValue GetArrayElement(
        int handle,
        int index)
    {
        ArrayObject arrayObject = GetRequiredArray(handle);

        if (index < 0 || index >= arrayObject.Elements.Length)
        {
            throw new InvalidOperationException("array index out of bounds");
        }

        return arrayObject.Elements[index];
    }

    public int GetArrayLength(
        int handle)
    {
        return GetRequiredArray(handle)
            .Elements.Length;
    }

    public HeapObjectKind GetHeapObjectKind(
        int handle)
    {
        return GetRequiredObject(handle)
            .Kind;
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

    public string GetString(
        int handle)
    {
        return GetRequiredString(handle)
            .Text;
    }

    public bool IsAliveHandle(
        int handle)
    {
        if (handle <= 0 || handle > _objects.Count)
        {
            return false;
        }

        return _objects[handle - 1] is not null;
    }

    public void SetArrayElement(
        int handle,
        int index,
        VmValue value)
    {
        ArrayObject arrayObject = GetRequiredArray(handle);

        if (index < 0 || index >= arrayObject.Elements.Length)
        {
            throw new InvalidOperationException("array index out of bounds");
        }

        arrayObject.Elements[index] = value;
    }

    public bool ShouldCollect()
    {
        return LiveObjectCount >= CollectionThreshold;
    }

    internal ArrayObject GetRequiredArray(
        int handle)
    {
        HeapObject heapObject = GetRequiredObject(handle);

        if (heapObject is not ArrayObject arrayObject)
        {
            throw new InvalidOperationException("heap handle does not reference an array");
        }

        return arrayObject;
    }

    internal StringObject GetRequiredString(
        int handle)
    {
        HeapObject heapObject = GetRequiredObject(handle);

        if (heapObject is not StringObject stringObject)
        {
            throw new InvalidOperationException("heap handle does not reference a string");
        }

        return stringObject;
    }

    private int AllocateObject(
        HeapObject heapObject)
    {
        int handle;

        if (_freeHandles.Count > 0)
        {
            handle = _freeHandles.Pop();
            _objects[handle - 1] = heapObject;
        }
        else
        {
            _objects.Add(heapObject);
            handle = _objects.Count;
        }

        TotalAllocations++;
        UpdatePeakLive();

        return handle;
    }

    private HeapObject GetRequiredObject(
        int handle)
    {
        if (handle <= 0 || handle > _objects.Count || _objects[handle - 1] is null)
        {
            throw new InvalidOperationException($"invalid heap handle '{handle}'");
        }

        return _objects[handle - 1]!;
    }

    private void MarkHandle(
        int handle,
        Stack<int> markStack)
    {
        HeapObject heapObject = GetRequiredObject(handle);

        if (heapObject.Marked)
        {
            return;
        }

        heapObject.Marked = true;
        markStack.Push(handle);
    }

    private void MarkValue(
        VmValue value,
        Stack<int> markStack)
    {
        if (value.Kind == VmValueKind.Ref)
        {
            MarkHandle(
                handle: value.AsHandle(),
                markStack: markStack);
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
