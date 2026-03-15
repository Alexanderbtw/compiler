using Compiler.Runtime.VM.Execution;

using Compiler.Runtime.VM;

namespace Compiler.Backend.CLR;

internal static class VmClrJitHelpers
{
    public static VmValue Get(
        VmValue[] values,
        int index)
    {
        return values[index];
    }

    public static void Set(
        VmValue[] values,
        int index,
        VmValue value)
    {
        values[index] = value;
    }

    public static long I64(
        VmValue value)
    {
        return value.AsInt64();
    }

    public static VmValue LoadIndex(
        VmValue array,
        VmValue index,
        IVmExecutionRuntime runtime)
    {
        return runtime.GetArrayElement(
            handle: array.AsHandle(),
            index: checked((int)index.AsInt64()));
    }

    public static void StoreIndex(
        VmValue array,
        VmValue index,
        VmValue value,
        IVmExecutionRuntime runtime)
    {
        runtime.SetArrayElement(
            handle: array.AsHandle(),
            index: checked((int)index.AsInt64()),
            value: value);
    }

    public static void ValidateArity(
        string functionName,
        int expected,
        int actual)
    {
        if (expected != actual)
        {
            throw new InvalidOperationException($"call to '{functionName}' expects {expected} args, got {actual}");
        }
    }
}
