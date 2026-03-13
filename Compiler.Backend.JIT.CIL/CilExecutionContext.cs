using Compiler.Execution;

namespace Compiler.Backend.JIT.CIL;

internal delegate Value CilJitFunc(
    CilExecutionContext ctx,
    Value[] args);

internal sealed class CilExecutionContext(
    IExecutionRuntime runtime)
{
    private readonly Dictionary<string, CilJitFunc> _functions = [];

    public IExecutionRuntime Runtime => runtime;

    public void EnterFrame(
        Value[] locals)
    {
        runtime.EnterFrame(locals);
    }

    public void ExitFrame()
    {
        runtime.ExitFrame();
    }

    public Value InvokeFunction(
        string name,
        Value[] args)
    {
        if (!_functions.TryGetValue(
                key: name,
                value: out CilJitFunc? fn))
        {
            throw new InvalidOperationException($"unknown function '{name}'");
        }

        return fn(
            ctx: this,
            args: args);
    }

    public void Register(
        string name,
        CilJitFunc fn)
    {
        _functions[name] = fn;
    }

    public bool TryGetFunction(
        string name,
        out CilJitFunc? fn)
    {
        return _functions.TryGetValue(
            key: name,
            value: out fn);
    }
}
