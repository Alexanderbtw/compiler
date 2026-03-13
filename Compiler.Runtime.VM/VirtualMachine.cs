using Compiler.Execution;
using Compiler.Runtime.VM.Execution.GC;
using Compiler.Runtime.VM.Options;

namespace Compiler.Runtime.VM;

/// <summary>
///     Minimal runtime host for JIT execution.
///     Holds the GC, tracks JIT frames as roots, and allocates VM arrays on demand.
/// </summary>
public sealed class VirtualMachine : IExecutionRuntime
{
    private readonly List<Func<IEnumerable<Value>>> _externalRootsProviders = [];
    private readonly GcHeap _gcHeap;

    // Active JIT frames locals used as GC roots
    private readonly Stack<Value[]> _jitCallLocals = new Stack<Value[]>(32);
    private readonly GcOptions _options;

    public VirtualMachine(
        GcOptions? options = null)
    {
        _options = options ?? GcOptions.Default;
        _gcHeap = new GcHeap(
            initialThreshold: _options.InitialThreshold,
            growthFactor: _options.GrowthFactor);
    }

    public VmArray AllocateArray(
        int length)
    {
        VmArray array = _gcHeap.AllocateArray(length);

        if (_options.AutoCollect && _gcHeap.ShouldCollect())
        {
            _gcHeap.Collect(EnumerateAllRoots());
        }

        return array;
    }

    public void EnterFrame(
        Value[] locals)
    {
        _jitCallLocals.Push(locals);
    }

    public void ExitFrame()
    {
        _jitCallLocals.Pop();
    }

    public GcStats GetGcStats()
    {
        return _gcHeap.GetStats();
    }

    public void RegisterExternalRootsProvider(
        Func<IEnumerable<Value>> provider)
    {
        if (provider is null)
        {
            throw new ArgumentNullException(nameof(provider));
        }

        _externalRootsProviders.Add(provider);
    }

    private IEnumerable<Value> EnumerateAllRoots()
    {
        foreach (Value v in _jitCallLocals.SelectMany(frameLocals => frameLocals))
        {
            yield return v;
        }

        foreach (Value v in _externalRootsProviders.SelectMany(provider => provider()))
        {
            yield return v;
        }
    }
}
