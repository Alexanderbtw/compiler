namespace Compiler.Tests.Backend;

public sealed class ManagedStringVmTests
{
    [Fact]
    public void StringLiteral_Executes_Through_VM_Runtime_String()
    {
        const string src = """
                           fn main() {
                               var s = "managed";
                               print(s);
                               return s;
                           }
                           """;

        (object? ret, string stdout) = TestUtils.RunVmMirJit(src);

        Assert.Equal(
            expected: "managed",
            actual: ret);

        Assert.Equal(
            expected: "managed",
            actual: stdout);
    }
}
