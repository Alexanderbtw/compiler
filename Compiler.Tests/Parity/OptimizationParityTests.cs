using Compiler.Frontend.Translation.MIR.Common;

namespace Compiler.Tests.Parity;

public sealed class OptimizationParityTests
{
    [Theory]
    [InlineData("factorial_calculation.minl")]
    [InlineData("array_sorting.minl")]
    [InlineData("prime_number_generation.minl")]
    public void Vm_O0_And_O1_Produce_Identical_Task_Results(
        string fileName)
    {
        string src = File.ReadAllText(
            Path.Combine(
                AppContext.BaseDirectory,
                "Tasks",
                fileName));

        (object? retO0, string outO0) = TestUtils.RunVmMirJit(
            src: src,
            level: MirOptimizationLevel.O0);
        (object? retO1, string outO1) = TestUtils.RunVmMirJit(
            src: src,
            level: MirOptimizationLevel.O1);

        TestUtils.AssertProgramResult(
            expectedRet: retO0,
            expectedStdout: outO0,
            actualRet: retO1,
            actualStdout: outO1);
    }

    [Fact]
    public void Vm_O0_And_O1_Preserve_Exception_Behavior()
    {
        const string src = """fn main() { assert(0, "boom"); }""";

        Exception exO0 = Assert.ThrowsAny<Exception>(() => TestUtils.RunVmMirJit(
            src: src,
            level: MirOptimizationLevel.O0));
        Exception exO1 = Assert.ThrowsAny<Exception>(() => TestUtils.RunVmMirJit(
            src: src,
            level: MirOptimizationLevel.O1));

        Assert.Contains(
            expectedSubstring: "boom",
            actualString: exO0.Message,
            comparisonType: StringComparison.OrdinalIgnoreCase);
        Assert.Equal(
            expected: exO0.Message,
            actual: exO1.Message);
    }
}
