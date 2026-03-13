namespace Compiler.Backend.JIT.Abstractions.Execution;

/// <summary>
///     Runtime services required by compiled programs.
///     Concrete runtimes remain free to expose richer APIs outside this contract.
/// </summary>
public interface IExecutionRuntime
{
    VmArray AllocateArray(
        int length);

    VmString AllocateString(
        string value);

    void EnterFrame(
        Value[] locals);

    void ExitFrame();
}
