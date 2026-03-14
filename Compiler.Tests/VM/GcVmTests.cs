using Compiler.Backend.VM;
using Compiler.Runtime.VM;
using Compiler.Runtime.VM.Execution.GC;
using Compiler.Runtime.VM.Options;

namespace Compiler.Tests.VM;

public sealed class GcVmTests
{
    [Fact]
    public void AutoCollect_Off_Suppresses_Collections()
    {
        var opts = new GcOptions
        {
            AutoCollect = false,
            InitialThreshold = 8,
            GrowthFactor = 1.5
        };

        var vm = new VirtualMachine(options: opts);
        var jit = new MirBackendCompiler();
        VmCompiledProgram program = jit.Compile(TestUtils.BuildMir(AllocLoopSrc));
        program.Execute(
            vm: vm,
            entryFunctionName: "main");

        GcStats s = vm.GetGcStats();
        Assert.True(s.TotalAllocations >= 64);
        Assert.Equal(
            expected: 0,
            actual: s.Collections);
    }

    [Fact]
    public void AutoCollect_On_Performs_Collections()
    {
        var opts = new GcOptions
        {
            AutoCollect = true,
            InitialThreshold = 8,
            GrowthFactor = 1.5
        };

        var vm = new VirtualMachine(options: opts);
        var jit = new MirBackendCompiler();
        VmCompiledProgram program = jit.Compile(TestUtils.BuildMir(AllocLoopSrc));
        program.Execute(
            vm: vm,
            entryFunctionName: "main");

        GcStats s = vm.GetGcStats();
        Assert.True(s.TotalAllocations >= 64);
        Assert.True(s.Collections >= 1);
    }

    private const string AllocLoopSrc =
        @"fn main(){
          var i = 0;
          while (i < 64) {
            var t = array(1);
            i = i + 1;
          }
          return 0;
        }";
}
