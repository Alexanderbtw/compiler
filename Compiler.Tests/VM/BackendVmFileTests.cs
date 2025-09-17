using Xunit.Abstractions;

namespace Compiler.Tests.VM;

public sealed class BackendVmFileTests(
    ITestOutputHelper testOutputHelper)
{
    [Theory]
    [Trait(
        name: "Category",
        value: "Tasks")]
    [ProgramFilesData]
    public void Backend_Vm_Executes_File(
        string path)
    {
        TestUtils.RunAndAssertFile(
            path: path,
            runner: TestUtils.RunVmMirJit,
            log: testOutputHelper);
    }

    // Removed redundant test which compared two VM runs to each other.
}
