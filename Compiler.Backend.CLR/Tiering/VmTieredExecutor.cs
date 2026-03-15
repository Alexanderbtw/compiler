using Compiler.Backend.VM;
using Compiler.Runtime.VM;
using Compiler.Runtime.VM.Bytecode;
using Compiler.Runtime.VM.Execution;

namespace Compiler.Backend.CLR.Tiering;

/// <summary>
///     Executes VM bytecode with hot-function promotion to the CLR-compiled tier.
/// </summary>
public sealed class VmTieredExecutor : IVmExecutionObserver
{
    private readonly VmClrJitCompiler _vmCompiler;
    private readonly VmJitOptions _options;
    private VmClrCompiledProgram? _compiledProgram;

    /// <summary>
    ///     Initializes a tiered executor.
    /// </summary>
    /// <param name="options">JIT policy options.</param>
    /// <param name="compiler">Optional CLR compiler instance.</param>
    public VmTieredExecutor(
        VmJitOptions? options = null,
        VmClrJitCompiler? compiler = null)
    {
        _options = options ?? new VmJitOptions();
        _vmCompiler = compiler ?? new VmClrJitCompiler();
    }

    /// <summary>
    ///     Active code-version registry.
    /// </summary>
    public CodeVersionRegistry Registry { get; } = new();

    /// <summary>
    ///     Executes a compiled bytecode program with tiered hot-function promotion.
    /// </summary>
    /// <param name="program">Baseline bytecode program.</param>
    /// <param name="vm">Backing VM/runtime.</param>
    /// <param name="entryFunctionName">Entry function name.</param>
    /// <returns>Program result.</returns>
    public VmValue Execute(
        VmCompiledProgram program,
        VirtualMachine vm,
        string entryFunctionName)
    {
        ArgumentNullException.ThrowIfNull(program);
        ArgumentNullException.ThrowIfNull(vm);
        ArgumentException.ThrowIfNullOrWhiteSpace(entryFunctionName);

        BaselineProgram = program.Program;

        return vm.Execute(
            program: BaselineProgram,
            entryFunctionName: entryFunctionName,
            observer: this);
    }

    /// <inheritdoc />
    public void OnFunctionEntered(
        VmFunction function)
    {
        Registry.GetOrAdd(function.Name)
            .RecordInvocation();
    }

    /// <inheritdoc />
    public void OnLoopBackEdge(
        VmFunction function,
        int sourceInstruction,
        int targetInstruction)
    {
        Registry.GetOrAdd(function.Name)
            .RecordLoopBackEdge();
    }

    /// <inheritdoc />
    public bool TryInvokeFunction(
        VirtualMachine vm,
        VmFunction function,
        VmValue[] arguments,
        out VmValue result)
    {
        ArgumentNullException.ThrowIfNull(vm);
        ArgumentNullException.ThrowIfNull(function);
        ArgumentNullException.ThrowIfNull(arguments);

        FunctionProfile profile = Registry.GetOrAdd(function.Name);

        if (profile.ActiveTargetKind == FunctionExecutionTargetKind.Baseline &&
            profile.InvocationCount >= _options.FunctionHotThreshold)
        {
            EnsureCompiledProgram();
            profile.MarkCompiled();
        }

        if (profile.ActiveTargetKind == FunctionExecutionTargetKind.ClrCompiled)
        {
            profile.RecordCompiledInvocation();
            result = _compiledProgram!.Invoke(
                runtime: vm,
                functionName: function.Name,
                args: arguments);

            return true;
        }

        result = default;
        return false;
    }

    private VmProgram BaselineProgram { get; set; } = null!;

    private void EnsureCompiledProgram()
    {
        _compiledProgram ??= _vmCompiler.Compile(BaselineProgram);
    }
}
