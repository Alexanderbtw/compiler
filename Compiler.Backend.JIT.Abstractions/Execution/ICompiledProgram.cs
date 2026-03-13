namespace Compiler.Backend.JIT.Abstractions.Execution;

/// <summary>
///     Executable artifact produced by a backend compiler.
/// </summary>
public interface ICompiledProgram
{
    Value Execute(
        IExecutionRuntime runtime,
        string entryFunctionName);
}
