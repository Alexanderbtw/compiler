using Compiler.Backend.VM;
using Compiler.Backend.VM.Execution;
using Compiler.Backend.VM.Values;
using Compiler.Frontend.Translation.HIR.Metadata;

namespace Compiler.Tests;

public sealed class BuiltinsConsistencyTests
{
    [Fact]
    public void All_Builtins_Have_Implementations_In_VM_And_Interpreter()
    {
        var vm = new VirtualMachine();
        var ctx = new VmJitContext(vm);

        foreach (KeyValuePair<string, List<BuiltinDescriptor>> kv in Builtins.Table)
        {
            string name = kv.Key;
            BuiltinDescriptor desc = kv.Value[0];

            // Build minimal, valid arguments for the builtin.
            // This is not exhaustive, but ensures presence of an implementation path.
            object?[] interpArgs = BuildInterpreterArgs(
                name: name,
                minArity: desc.MinArity);

            Value[] vmArgs = BuildVmArgs(
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
                _ = BuiltinsVm.Invoke(
                    name: name,
                    ctx: ctx,
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

    private static Value[] BuildVmArgs(
        string name,
        int minArity)
    {
        return name switch
        {
            "print" => [Value.FromLong(0)],
            "assert" => [Value.FromBool(true)],
            "array" => [Value.FromLong(0)],
            "clock_ms" => [],
            "len" => [Value.FromString("x")],
            "ord" => [Value.FromChar('A')],
            "chr" => [Value.FromLong(65)],
            _ => Enumerable
                .Repeat(
                    element: Value.FromLong(0),
                    count: Math.Max(
                        val1: minArity,
                        val2: 0))
                .ToArray()
        };
    }
}
