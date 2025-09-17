using Compiler.Backend.VM.Execution.GC;
using Compiler.Backend.VM.Options;
using Compiler.Backend.VM.Values;

namespace Compiler.Backend.VM;

/// <summary>
///     Minimal runtime host for JIT execution.
///     Holds the GC, tracks JIT frames as roots, and allocates VM arrays on demand.
/// </summary>
public sealed class VirtualMachine
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

    public VmArray AllocateArrayFromJit(
        int length)
    {
        VmArray array = _gcHeap.AllocateArray(length);

        if (_options.AutoCollect && _gcHeap.ShouldCollect())
        {
            _gcHeap.Collect(EnumerateAllRoots());
        }

        return array;
    }

    public GcStats GetGcStats()
    {
        return _gcHeap.GetStats();
    }

    public void JitEnterFunction(
        Value[] locals)
    {
        _jitCallLocals.Push(locals);
    }

    public void JitExitFunction()
    {
        _jitCallLocals.Pop();
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
