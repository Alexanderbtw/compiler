namespace Compiler.Tests.Parity;

public sealed class BackendLlvmParityTests
{
    [Fact]
    public void Arrays_Indexing_Parity_Including_LLVM()
    {
        string src = @"fn main() {
            var a = array(5, 1);
            a[2] = 42;
            print(len(a), a[0], a[2], a[4]);
        }";

        (object? iRet, string iOut) = TestUtils.RunInterpreter(src);
        (object? vRet, string vOut) = TestUtils.RunVmMirJit(src);
        (object? lRet, string lOut) = TestUtils.RunLlvmJit(src);

        Assert.Equal(
            expected: iOut,
            actual: vOut);

        Assert.Equal(
            expected: iOut,
            actual: lOut);

        Assert.Null(iRet);
    }

    [Fact]
    public void Assert_WithMessage_Throws_Including_LLVM()
    {
        string src = "fn main() { assert(0, \"boom\"); }";

        void AssertThrows(
            Func<string, (object? ret, string stdout)> runner)
        {
            var ex = Assert.ThrowsAny<Exception>(() => runner(src));
            Assert.Contains(
                expectedSubstring: "boom",
                actualString: ex.Message,
                comparisonType: StringComparison.OrdinalIgnoreCase);
        }

        AssertThrows(TestUtils.RunInterpreter);
        AssertThrows(TestUtils.RunVmMirJit);
        AssertThrows(TestUtils.RunLlvmJit);
    }

    [Fact]
    public void Len_Strings_And_Arrays_Across_Backends()
    {
        string src = """
                     fn main() {
                                 var a = array(3, 0);
                                 print(len("abcd"), len(a));
                             }
                     """;

        (object? iRet, string iOut) = TestUtils.RunInterpreter(src);
        (object? vRet, string vOut) = TestUtils.RunVmMirJit(src);
        (object? lRet, string lOut) = TestUtils.RunLlvmJit(src);

        Assert.Equal(
            expected: iOut,
            actual: vOut);

        Assert.Equal(
            expected: iOut,
            actual: lOut);

        Assert.Null(iRet);
    }
    [Fact]
    public void Print_Strings_And_Numbers_Across_Backends()
    {
        string src = """
                     fn main() {
                                 var s = "hello";
                                 print(s, 123, "world");
                             }
                     """;

        (object? iRet, string iOut) = TestUtils.RunInterpreter(src);
        (object? vRet, string vOut) = TestUtils.RunVmMirJit(src);
        (object? lRet, string lOut) = TestUtils.RunLlvmJit(src);

        Assert.Equal(
            expected: iOut,
            actual: vOut);

        Assert.Equal(
            expected: iOut,
            actual: lOut);

        Assert.Null(iRet);
    }
}
