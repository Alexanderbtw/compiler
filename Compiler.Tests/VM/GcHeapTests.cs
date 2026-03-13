using Compiler.Backend.JIT.Abstractions.Execution;
using Compiler.Runtime.VM.Execution.GC;

namespace Compiler.Tests.VM;

public sealed class GcHeapTests
{
    [Fact]
    public void Collect_Keeps_Strings_Reachable_Through_Arrays()
    {
        var heap = new GcHeap(
            initialThreshold: 16,
            growthFactor: 1.5);

        VmArray root = heap.AllocateArray(1);
        VmString kept = heap.AllocateString("kept");
        VmString dropped = heap.AllocateString("dropped");
        root[0] = Value.FromString(kept);

        Assert.Equal(
            expected: 2,
            actual: heap.LiveStringCount);

        heap.Collect([Value.FromArray(root)]);

        Assert.Equal(
            expected: 1,
            actual: heap.LiveStringCount);

        Assert.Equal(
            expected: 2,
            actual: heap.LiveObjectCount);
    }
    [Fact]
    public void Collect_Removes_Unreachable_Arrays()
    {
        var heap = new GcHeap(
            initialThreshold: 16,
            growthFactor: 1.5);

        // Allocate 3 arrays, keep only the second one reachable
        VmArray a1 = heap.AllocateArray(1);
        VmArray a2 = heap.AllocateArray(2);
        VmArray a3 = heap.AllocateArray(3);

        Assert.Equal(
            expected: 3,
            actual: heap.LiveArrayCount);

        // Only root a2
        Value[] roots = [Value.FromArray(a2)];
        heap.Collect(roots);

        Assert.Equal(
            expected: 1,
            actual: heap.LiveArrayCount);

        GcStats stats = heap.GetStats();
        Assert.Equal(
            expected: 3,
            actual: stats.TotalAllocations);

        Assert.Equal(
            expected: 1,
            actual: stats.Collections);

        Assert.True(stats.PeakLive >= 3);
    }

    [Fact]
    public void Collect_Removes_Unreachable_Strings()
    {
        var heap = new GcHeap(
            initialThreshold: 16,
            growthFactor: 1.5);

        VmString s1 = heap.AllocateString("a");
        VmString s2 = heap.AllocateString("b");
        VmString s3 = heap.AllocateString("c");

        Assert.Equal(
            expected: 3,
            actual: heap.LiveStringCount);

        Assert.Equal(
            expected: 3,
            actual: heap.LiveObjectCount);

        heap.Collect([Value.FromString(s2)]);

        Assert.Equal(
            expected: 1,
            actual: heap.LiveStringCount);

        Assert.Equal(
            expected: 1,
            actual: heap.LiveObjectCount);

        GcStats stats = heap.GetStats();
        Assert.Equal(
            expected: 3,
            actual: stats.TotalAllocations);

        Assert.Equal(
            expected: 1,
            actual: stats.Collections);

        Assert.True(stats.PeakLive >= 3);
    }

    [Fact]
    public void Threshold_Grows_When_Many_Live()
    {
        var initial = 16;
        var growth = 1.5;
        var heap = new GcHeap(
            initialThreshold: initial,
            growthFactor: growth);

        // Create a root array that will reference many children to keep them live
        VmArray root = heap.AllocateArray(initial);

        for (var i = 0; i < initial; i++)
        {
            root[i] = Value.FromArray(heap.AllocateArray(1));
        }

        // All arrays are reachable (root + children)
        heap.Collect([Value.FromArray(root)]);

        Assert.True(heap.CollectionThreshold >= (int)Math.Ceiling(initial * growth));
        Assert.True(heap.LiveArrayCount >= initial); // root + many children remain live
        Assert.True(heap.LiveObjectCount >= initial);
    }
}
