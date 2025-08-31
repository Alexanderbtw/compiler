using Xunit.Abstractions;

namespace Compiler.Tests.VM;

public class BackendVmFileTests(ITestOutputHelper testOutputHelper)
{
    [Theory]
    [ProgramFilesData]
    public void Backend_Vm_Executes_File(string path) => TestUtils.RunAndAssertFile(
        path,
        TestUtils.RunVm,
        testOutputHelper);

    [Theory]
    [ProgramFilesData]
    public void Vm_vs_Cil_All_Files(string path)
    {
        string src = File.ReadAllText(path);
        (object? retExpected, string outExpected) = TestUtils.RunCil(src);
        (object? retActual, string outActual) = TestUtils.RunVm(src);
        TestUtils.AssertProgramResult(
            retExpected,
            outExpected,
            retActual,
            outActual,
            testOutputHelper);
    }
}
