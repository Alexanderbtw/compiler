namespace Compiler.Tests.Parity;

public sealed class BackendParityTests
{
    [Fact]
    public void ArrayInit_FillsElements_ParityAcrossBackends()
    {
        string src = @"fn main() {
            var a = array(5, 42);
            assert(len(a) == 5);
            assert(a[0] == 42);
            assert(a[4] == 42);
            print(len(a));
            print(a[0]);
            print(a[4]);
        }";

        (object? iRet, string iOut) = TestUtils.RunInterpreter(src);
        (object? cRet, string cOut) = TestUtils.RunCil(src);
        (object? mRet, string mOut) = TestUtils.RunVmMirJit(src);

        Assert.Null(iRet);
        Assert.Equal(
            expected: iRet,
            actual: cRet);

        Assert.Equal(
            expected: iRet,
            actual: mRet);

        Assert.Equal(
            expected: iOut,
            actual: cOut);

        Assert.Equal(
            expected: iOut,
            actual: mOut);
    }

    [Fact]
    public void Assert_WithMessage_ThrowsAcrossBackends()
    {
        string src = @"fn main() {
            assert(0, 'boom');
        }";

        void AssertThrows(
            Func<string, (object? ret, string stdout)> runner)
        {
            var ex = Assert.ThrowsAny<Exception>(() => { runner(src); });
            Assert.Contains(
                expectedSubstring: "boom",
                actualString: ex.Message,
                comparisonType: StringComparison.OrdinalIgnoreCase);
        }

        AssertThrows(TestUtils.RunInterpreter);
        AssertThrows(TestUtils.RunCil);
        AssertThrows(TestUtils.RunVmMirJit);
    }

    [Fact]
    public void ChrOrd_Roundtrip_ParityAcrossBackends()
    {
        string src = @"fn main() {
            var c = chr(ord('Z'));
            assert(c == 'Z');
            print(c);
            return c;
        }";

        (object? iRet, string iOut) = TestUtils.RunInterpreter(src);
        (object? cRet, string cOut) = TestUtils.RunCil(src);
        (object? mRet, string mOut) = TestUtils.RunVmMirJit(src);

        Assert.Equal(
            expected: iRet,
            actual: cRet);

        Assert.Equal(
            expected: iRet,
            actual: mRet);

        Assert.Equal(
            expected: iOut,
            actual: cOut);

        Assert.Equal(
            expected: iOut,
            actual: mOut);
    }
}
