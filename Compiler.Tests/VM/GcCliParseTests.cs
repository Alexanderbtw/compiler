using Compiler.Backend.VM.Options;

namespace Compiler.Tests.VM;

public sealed class GcCliParseTests
{
    [Fact]
    public void Defaults_When_No_Flags()
    {
        (GcOptions o, bool stats) = GcCliArgs.Parse([]);
        Assert.True(o.AutoCollect);
        Assert.Equal(
            expected: 1024,
            actual: o.InitialThreshold);

        Assert.Equal(
            expected: 2.0,
            actual: o.GrowthFactor);

        Assert.False(stats);
    }

    [Fact]
    public void Parses_All_Flags()
    {
        string[] args =
        [
            "--vm-gc-threshold=64",
            "--vm-gc-growth=1.25",
            "--vm-gc-auto=off",
            "--vm-gc-stats"
        ];

        (GcOptions o, bool stats) = GcCliArgs.Parse(args);
        Assert.False(o.AutoCollect);
        Assert.Equal(
            expected: 64,
            actual: o.InitialThreshold);

        Assert.Equal(
            expected: 1.25,
            actual: o.GrowthFactor,
            precision: 3);

        Assert.True(stats);
    }

    [Theory]
    [InlineData(
        "off",
        false)]
    [InlineData(
        "false",
        false)]
    [InlineData(
        "0",
        false)]
    [InlineData(
        "on",
        true)]
    [InlineData(
        "true",
        true)]
    [InlineData(
        "1",
        true)]
    public void Parses_Auto_Variants(
        string val,
        bool expected)
    {
        string[] args = ["--vm-gc-auto=" + val];
        (GcOptions o, _) = GcCliArgs.Parse(args);
        Assert.Equal(
            expected: expected,
            actual: o.AutoCollect);
    }
}
