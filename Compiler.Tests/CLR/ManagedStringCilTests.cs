namespace Compiler.Tests.CLR;

public sealed class ManagedStringCilTests
{
    [Fact]
    public void StringLiteral_Executes_Through_Managed_Runtime_String()
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
