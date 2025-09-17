using Xunit.Abstractions;

namespace Compiler.Tests.Interpretation;

public sealed class InterpreterFileTests(
    ITestOutputHelper testOutputHelper)
{
    [Theory]
    [Trait(
        name: "Category",
        value: "Tasks")]
    [ProgramFilesData]
    public void Interpreter_Executes_File(
        string path)
    {
        TestUtils.RunAndAssertFile(
            path: path,
            runner: TestUtils.RunInterpreter,
            log: testOutputHelper);
    }
}
