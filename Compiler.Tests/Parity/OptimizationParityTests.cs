using Compiler.Frontend.Translation.MIR.Common;

namespace Compiler.Tests.Parity;

public sealed class OptimizationParityTests
{
    [Fact]
    public void Vm_LocalOnly_And_StableDefault_Produce_Identical_Result_For_Safe_Program()
    {
        const string src = """
                           fn main() {
                               var x = 1 + 2;
                               if (x == 3) {
                                   print(x);
                               }
                               return x;
                           }
                           """;

        (object? retLocal, string outLocal) = TestUtils.RunVmMirJit(
            src: src,
            enabledPasses: LocalOnly);

        (object? retDefault, string outDefault) = TestUtils.RunVmMirJit(
            src: src,
            enabledPasses: MirOptimizationPasses.StableDefault);

        TestUtils.AssertProgramResult(
            expectedRet: retLocal,
            expectedStdout: outLocal,
            actualRet: retDefault,
            actualStdout: outDefault);
    }

    [Fact]
    public void Vm_NoPasses_And_StableDefault_Preserve_Exception_Behavior()
    {
        const string src = """fn main() { assert(0, "boom"); }""";

        var exBaseline = Assert.ThrowsAny<Exception>(() => TestUtils.RunVmMirJit(
            src: src,
            enabledPasses: MirOptimizationPasses.None));

        var exOptimized = Assert.ThrowsAny<Exception>(() => TestUtils.RunVmMirJit(
            src: src,
            enabledPasses: MirOptimizationPasses.StableDefault));

        Assert.Contains(
            expectedSubstring: "boom",
            actualString: exBaseline.Message,
            comparisonType: StringComparison.OrdinalIgnoreCase);

        Assert.Equal(
            expected: exBaseline.Message,
            actual: exOptimized.Message);
    }

    [Theory]
    [InlineData("factorial_calculation.minl")]
    [InlineData("array_sorting.minl")]
    [InlineData("prime_number_generation.minl")]
    public void Vm_NoPasses_And_StableDefault_Produce_Identical_Task_Results(
        string fileName)
    {
        string src = File.ReadAllText(
            Path.Combine(
                path1: AppContext.BaseDirectory,
                path2: "Tasks",
                path3: fileName));

        (object? retBaseline, string outBaseline) = TestUtils.RunVmMirJit(
            src: src,
            enabledPasses: MirOptimizationPasses.None);

        (object? retOptimized, string outOptimized) = TestUtils.RunVmMirJit(
            src: src,
            enabledPasses: MirOptimizationPasses.StableDefault);

        TestUtils.AssertProgramResult(
            expectedRet: retBaseline,
            expectedStdout: outBaseline,
            actualRet: retOptimized,
            actualStdout: outOptimized);
    }

    private const MirOptimizationPasses LocalOnly = MirOptimizationPasses.LocalConstantFolding |
        MirOptimizationPasses.AlgebraicSimplification |
        MirOptimizationPasses.PeepholeOptimization |
        MirOptimizationPasses.LocalCopyPropagation;
}
