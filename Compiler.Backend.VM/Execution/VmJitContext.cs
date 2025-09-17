using Compiler.Backend.VM.Values;

namespace Compiler.Backend.VM.Execution;

/// <summary>
///     Per-execution VM context for JITted code
/// </summary>
public delegate Value VmJitFunc(
    VmJitContext ctx,
    Value[] args);

public sealed class VmJitContext(
    VirtualMachine vm)
{
    private readonly Dictionary<string, VmJitFunc> _functions = [];

    public IReadOnlyDictionary<string, VmJitFunc> Functions => _functions;

    public VmArray AllocArray(
        int length)
    {
        return vm.AllocateArrayFromJit(length);
    }

    public void EnterFrame(
        Value[] locals)
    {
        vm.JitEnterFunction(locals);
    }

    public void ExitFrame()
    {
        vm.JitExitFunction();
    }

    public Value InvokeFunction(
        string name,
        Value[] args)
    {
        if (!_functions.TryGetValue(
                key: name,
                value: out VmJitFunc? fn))
        {
            throw new InvalidOperationException($"unknown function '{name}'");
        }

        return fn(
            ctx: this,
            args: args);
    }

    public void Register(
        string name,
        VmJitFunc fn)
    {
        _functions[name] = fn;
    }
}
