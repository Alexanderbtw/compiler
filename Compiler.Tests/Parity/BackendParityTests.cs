namespace Compiler.Tests.Parity;

public sealed class BackendParityTests
{
    [Fact]
    public void ArrayInit_FillsElements_ParityAcrossBackends()
    {
        var src = @"fn main() {
            var a = array(5, 42);
            assert(len(a) == 5);
            assert(a[0] == 42);
            assert(a[4] == 42);
            print(len(a));
            print(a[0]);
            print(a[4]);
        }";

        (object? iRet, string iOut) = TestUtils.RunInterpreter(src);
        (object? mRet, string mOut) = TestUtils.RunVmMirJit(src);

        Assert.Null(iRet);
        Assert.Equal(
            expected: iRet,
            actual: mRet);

        ;

        Assert.Equal(
            expected: iOut,
            actual: mOut);
    }

    [Fact]
    public void Assert_WithMessage_ThrowsAcrossBackends()
    {
        var src = "fn main() { assert(0, \"boom\"); }";

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
        AssertThrows(TestUtils.RunVmMirJit);
    }

    [Fact]
    public void BlockShadowing_ParityAcrossBackends()
    {
        var src = @"fn main() {
            var x = 1;
            {
                var x = 2;
                print(x);
            }
            print(x);
        }";

        (object? iRet, string iOut) = TestUtils.RunInterpreter(src);
        (object? mRet, string mOut) = TestUtils.RunVmMirJit(src);

        Assert.Equal(
            expected: iRet,
            actual: mRet);

        Assert.Equal(
            expected: iOut,
            actual: mOut);
    }

    [Fact]
    public void ChrOrd_Roundtrip_ParityAcrossBackends()
    {
        var src = @"fn main() {
            var c = chr(ord('Z'));
            assert(c == 'Z');
            print(c);
            return c;
        }";

        (object? iRet, string iOut) = TestUtils.RunInterpreter(src);
        (object? mRet, string mOut) = TestUtils.RunVmMirJit(src);

        Assert.Equal(
            expected: iRet,
            actual: mRet);

        Assert.Equal(
            expected: iOut,
            actual: mOut);
    }

    [Fact]
    public void EmptyStringTruthiness_ParityAcrossBackends()
    {
        var src = @"fn main() {
            if ("""") {
                print(1);
            }
            else {
                print(2);
            }
        }";

        (object? iRet, string iOut) = TestUtils.RunInterpreter(src);
        (object? mRet, string mOut) = TestUtils.RunVmMirJit(src);

        Assert.Equal(
            expected: iRet,
            actual: mRet);

        Assert.Equal(
            expected: iOut,
            actual: mOut);
    }

    [Fact]
    public void StringBuiltins_ParityAcrossBackends()
    {
        var src = @"fn main() {
            var s = ""hi"";
            assert(len(s) == 2);
            assert(ord(""Z"") == 90);
            assert(s == ""hi"");
            print(s);
            return s;
        }";

        (object? iRet, string iOut) = TestUtils.RunInterpreter(src);
        (object? mRet, string mOut) = TestUtils.RunVmMirJit(src);

        Assert.Equal(
            expected: iRet,
            actual: mRet);

        Assert.Equal(
            expected: iOut,
            actual: mOut);
    }
}
