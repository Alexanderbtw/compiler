using Compiler.Backend.VM.Execution.GC;
using Compiler.Backend.VM.Options;
using Compiler.Backend.VM.Values;

namespace Compiler.Backend.VM;

/// <summary>
///     Minimal VM runtime host for JIT execution: provides GC and roots management.
///     No bytecode interpreter is present; MIR → IL JIT executes functions and uses
///     these hooks to allocate arrays and expose GC roots.
/// </summary>
public sealed class VirtualMachine
{
    private readonly GcHeap _gcHeap;

    // Active JIT frames' locals used as GC roots
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
            _gcHeap.Collect(EnumerateJitRoots());
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

    private IEnumerable<Value> EnumerateJitRoots()
    {
        foreach (Value[] frameLocals in _jitCallLocals)
        {
            foreach (Value v in frameLocals)
            {
                yield return v;
            }
        }
    }
}
