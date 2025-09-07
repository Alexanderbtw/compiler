using Compiler.Backend.VM;
using Compiler.Backend.VM.Execution.GC;
using Compiler.Backend.VM.Options;
using Compiler.Backend.VM.Values;

using Xunit.Abstractions;

namespace Compiler.Tests.VM;

public sealed class GcModesStatsTests(
    ITestOutputHelper output)
{
    [Theory]
    [InlineData(
        true,
        8,
        1.5)]
    [InlineData(
        false,
        8,
        1.5)]
    public void Show_Gc_Statistics_For_Modes(
        bool auto,
        int thr,
        double growth)
    {
        VmModule bytecode = TestUtils.BuildBytecode(AllocLoopSrc);
        var opts = new GcOptions { AutoCollect = auto, InitialThreshold = thr, GrowthFactor = growth };
        var vm = new VirtualMachine(
            module: bytecode,
            options: opts);

        vm.Execute();

        GcStats s = vm.GetGcStats();
        output.WriteLine(
            format: "mode=auto:{0} threshold={1} growth={2}",
            auto
                ? "on"
                : "off",
            s.Threshold,
            s.GrowthFactor);

        output.WriteLine(
            format: "allocs={0} colls={1} live={2} peak={3}",
            s.TotalAllocations,
            s.Collections,
            s.Live,
            s.PeakLive);

        Assert.True(s.TotalAllocations >= 32);

        if (auto)
        {
            Assert.True(s.Collections >= 1);
        }
        else
        {
            Assert.Equal(
                expected: 0,
                actual: s.Collections);
        }
    }

    private const string AllocLoopSrc =
        @"fn main() {
          var i = 0;
          while (i < 32) {
            var t = array(1);
            i = i + 1;
          }
          return 0;
        }";
}
