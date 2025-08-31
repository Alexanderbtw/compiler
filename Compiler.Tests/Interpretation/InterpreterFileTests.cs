using Xunit.Abstractions;

namespace Compiler.Tests.Interpretation;

public class InterpreterFileTests(
    ITestOutputHelper testOutputHelper)
{
    [Theory]
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
