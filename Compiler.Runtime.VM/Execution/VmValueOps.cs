namespace Compiler.Runtime.VM.Execution;

/// <summary>
///     Dynamic value operations shared by the VM and runtime builtins.
/// </summary>
public static class VmValueOps
{
    public static bool AreEqual(
        VmValue left,
        VmValue right,
        VirtualMachine vm)
    {
        if (left.Kind != right.Kind)
        {
            return left.Kind switch
            {
                VmValueKind.I64 when right.Kind == VmValueKind.Char => left.AsInt64() == right.AsChar(),
                VmValueKind.Char when right.Kind == VmValueKind.I64 => left.AsChar() == right.AsInt64(),
                _ => false
            };
        }

        return left.Kind switch
        {
            VmValueKind.Null => true,
            VmValueKind.I64 => left.AsInt64() == right.AsInt64(),
            VmValueKind.Bool => left.AsBool() == right.AsBool(),
            VmValueKind.Char => left.AsChar() == right.AsChar(),
            VmValueKind.Ref => AreReferencesEqual(
                leftHandle: left.AsHandle(),
                rightHandle: right.AsHandle(),
                vm: vm),
            _ => throw new ArgumentOutOfRangeException()
        };
    }

    public static VmValue Len(
        VmValue value,
        VirtualMachine vm)
    {
        return vm.GetHeapObjectKind(value.AsHandle()) switch
        {
            HeapObjectKind.String => VmValue.FromLong(
                vm.GetString(value.AsHandle())
                    .Length),
            HeapObjectKind.Array => VmValue.FromLong(vm.GetArrayLength(value.AsHandle())),
            _ => throw new ArgumentOutOfRangeException()
        };
    }

    public static bool ToBool(
        VmValue value,
        VirtualMachine vm)
    {
        return value.Kind switch
        {
            VmValueKind.Null => false,
            VmValueKind.I64 => value.AsInt64() != 0,
            VmValueKind.Bool => value.AsBool(),
            VmValueKind.Char => value.AsChar() != '\0',
            VmValueKind.Ref => vm.GetHeapObjectKind(value.AsHandle()) switch
            {
                HeapObjectKind.String => !string.IsNullOrEmpty(vm.GetString(value.AsHandle())),
                HeapObjectKind.Array => vm.GetArrayLength(value.AsHandle()) != 0,
                _ => true
            },
            _ => throw new ArgumentOutOfRangeException()
        };
    }

    private static bool AreReferencesEqual(
        int leftHandle,
        int rightHandle,
        VirtualMachine vm)
    {
        HeapObjectKind leftKind = vm.GetHeapObjectKind(leftHandle);
        HeapObjectKind rightKind = vm.GetHeapObjectKind(rightHandle);

        if (leftKind != rightKind)
        {
            return false;
        }

        return leftKind switch
        {
            HeapObjectKind.String => string.Equals(
                a: vm.GetString(leftHandle),
                b: vm.GetString(rightHandle),
                comparisonType: StringComparison.Ordinal),
            HeapObjectKind.Array => leftHandle == rightHandle,
            _ => throw new ArgumentOutOfRangeException()
        };
    }
}
