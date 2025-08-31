using Xunit.Abstractions;

namespace Compiler.Tests.CLR;

public class BackendCilFileTests(
    ITestOutputHelper testOutputHelper)
{
    [Theory]
    [ProgramFilesData]
    public void Backend_CIL_Executes_File(
        string path)
    {
        TestUtils.RunAndAssertFile(
            path: path,
            runner: TestUtils.RunCil,
            log: testOutputHelper);
    }

    [Theory]
    [ProgramFilesData]
    public void Cil_vs_Interp_All_Files(
        string path)
    {
        string src = File.ReadAllText(path);
        (object? retExpected, string outExpected) = TestUtils.RunInterpreter(src);
        (object? retActual, string outActual) = TestUtils.RunCil(src);
        TestUtils.AssertProgramResult(
            expectedRet: retExpected,
            expectedStdout: outExpected,
            actualRet: retActual,
            actualStdout: outActual,
            log: testOutputHelper);
    }
}
