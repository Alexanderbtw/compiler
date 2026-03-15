using Compiler.Runtime.VM;
using Compiler.Runtime.VM.Execution;

namespace Compiler.Backend.CLR;

internal delegate VmValue VmClrJitFunc(
    VmClrExecutionContext context,
    VmValue[] args);

internal sealed class VmClrExecutionContext(
    IVmExecutionRuntime runtime)
{
    private int _activeFrameCount;
    private readonly Dictionary<string, VmClrJitFunc> _functions = new Dictionary<string, VmClrJitFunc>(StringComparer.Ordinal);

    public IVmExecutionRuntime Runtime { get; } = runtime;

    public void EnterFrame(
        VmValue[] locals,
        VmValue[] constants)
    {
        Runtime.EnterCompiledFrame(
            locals: locals,
            constants: constants);
        _activeFrameCount++;
    }

    public void ExitFrame()
    {
        if (_activeFrameCount <= 0)
        {
            throw new InvalidOperationException("no compiled frame is active");
        }

        Runtime.ExitCompiledFrame();
        _activeFrameCount--;
    }

    public void ResetFrames()
    {
        while (_activeFrameCount > 0)
        {
            Runtime.ExitCompiledFrame();
            _activeFrameCount--;
        }
    }

    public VmValue InvokeFunction(
        string name,
        VmValue[] args)
    {
        if (!_functions.TryGetValue(
                key: name,
                value: out VmClrJitFunc? function))
        {
            throw new InvalidOperationException($"unknown function '{name}'");
        }

        return function(
            context: this,
            args: args);
    }

    public void Register(
        string name,
        VmClrJitFunc function)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(function);

        _functions[name] = function;
    }

    public bool TryGetFunction(
        string name,
        out VmClrJitFunc? function)
    {
        return _functions.TryGetValue(
            key: name,
            value: out function);
    }
}
