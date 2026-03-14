using Compiler.Frontend.Translation.HIR.Metadata;
using Compiler.Runtime.VM;
using Compiler.Runtime.VM.Execution;

namespace Compiler.Tests;

public sealed class BuiltinsConsistencyTests
{
    [Fact]
    public void All_Builtins_Have_Implementations_In_VM_And_Interpreter()
    {
        var vm = new VirtualMachine();

        foreach (KeyValuePair<string, List<BuiltinDescriptor>> kv in Builtins.Table)
        {
            string name = kv.Key;
            BuiltinDescriptor desc = kv.Value[0];

            // Build minimal, valid arguments for the builtin.
            // This is not exhaustive, but ensures presence of an implementation path.
            object?[] interpArgs = BuildInterpreterArgs(
                name: name,
                minArity: desc.MinArity);

            VmValue[] vmArgs = BuildVmArgs(
                vm: vm,
                name: name,
                minArity: desc.MinArity);

            // Interpreter path
            bool ok = Interpreter.Builtins.TryInvoke(
                name: name,
                args: interpArgs,
                result: out _);

            Assert.True(
                condition: ok,
                userMessage: $"Interpreter missing builtin '{name}'");

            // VM runtime path
            try
            {
                _ = VmBuiltins.Invoke(
                    name: name,
                    vm: vm,
                    args: vmArgs);
            }
            catch (Exception ex)
            {
                Assert.Fail($"VM missing or failing builtin '{name}': {ex.Message}");
            }
        }
    }

    private static object?[] BuildInterpreterArgs(
        string name,
        int minArity)
    {
        return name switch
        {
            "print" => [0L],
            "assert" => [true],
            "array" => [0L],
            "clock_ms" => [],
            "len" => ["x"],
            "ord" => ['A'],
            "chr" => [65L],
            _ => Enumerable
                .Repeat<object?>(
                    element: 0L,
                    count: Math.Max(
                        val1: minArity,
                        val2: 0))
                .ToArray()
        };
    }

    private static VmValue[] BuildVmArgs(
        VirtualMachine vm,
        string name,
        int minArity)
    {
        return name switch
        {
            "print" => [VmValue.FromLong(0)],
            "assert" => [VmValue.FromBool(true)],
            "array" => [VmValue.FromLong(0)],
            "clock_ms" => [],
            "len" => [vm.AllocateString("x")],
            "ord" => [VmValue.FromChar('A')],
            "chr" => [VmValue.FromLong(65)],
            _ => Enumerable
                .Repeat(
                    element: VmValue.FromLong(0),
                    count: Math.Max(
                        val1: minArity,
                        val2: 0))
                .ToArray()
        };
    }
}
