using Xunit.Abstractions;

namespace Compiler.Tests.VM;

public sealed class BackendVmFileTests(
    ITestOutputHelper testOutputHelper)
{
    [Theory]
    [ProgramFilesData]
    public void Backend_Vm_Executes_File(
        string path)
    {
        TestUtils.RunAndAssertFile(
            path: path,
            runner: TestUtils.RunVm,
            log: testOutputHelper);
    }

    [Theory]
    [ProgramFilesData]
    public void Vm_vs_Cil_All_Files(
        string path)
    {
        string src = File.ReadAllText(path);
        (object? retExpected, string outExpected) = TestUtils.RunCil(src);
        (object? retActual, string outActual) = TestUtils.RunVm(src);
        TestUtils.AssertProgramResult(
            expectedRet: retExpected,
            expectedStdout: outExpected,
            actualRet: retActual,
            actualStdout: outActual,
            log: testOutputHelper);
    }
}
