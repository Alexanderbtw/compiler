using Xunit.Abstractions;

namespace Compiler.Tests.CLR;

public sealed class BackendCilFileTests(
    ITestOutputHelper testOutputHelper)
{
    [Theory]
    [Trait(
        name: "Category",
        value: "Tasks")]
    [ProgramFilesData]
    public void Backend_CIL_Executes_File(
        string path)
    {
        TestUtils.RunAndAssertFile(
            path: path,
            runner: TestUtils.RunVmMirJit,
            log: testOutputHelper);
    }

    [Theory]
    [Trait(
        name: "Category",
        value: "Tasks")]
    [ProgramFilesData]
    public void Cil_vs_Interp_All_Files(
        string path)
    {
        string src = File.ReadAllText(path);
        (object? retExpected, string outExpected) = TestUtils.RunInterpreter(src);
        (object? retActual, string outActual) = TestUtils.RunVmMirJit(src);
        TestUtils.AssertProgramResult(
            expectedRet: retExpected,
            expectedStdout: outExpected,
            actualRet: retActual,
            actualStdout: outActual,
            log: testOutputHelper);
    }
}
