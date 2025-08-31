using Xunit.Abstractions;

namespace Compiler.Tests.CLR;

public class BackendCilFileTests(ITestOutputHelper testOutputHelper)
{
    [Theory]
    [ProgramFilesData]
    public void Backend_CIL_Executes_File(string path) => TestUtils.RunAndAssertFile(
        path,
        TestUtils.RunCil,
        testOutputHelper);
}
