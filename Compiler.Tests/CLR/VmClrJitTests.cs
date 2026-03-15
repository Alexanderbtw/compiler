using Compiler.Backend.CLR;
using Compiler.Core.Builtins;
using Compiler.Frontend.Translation.MIR.Common;
using Compiler.Runtime.VM;

namespace Compiler.Tests.CLR;

public sealed class VmClrJitTests
{
    [Fact]
    public void JitProgram_MatchesVm_ForRecursiveProgram()
    {
        const string source = """
                              fn fact(n) {
                                  if (n <= 1) {
                                      return 1;
                                  }

                                  return n * fact(n - 1);
                              }

                              fn main() {
                                  return fact(8);
                              }
                              """;

        MirModule mir = TestUtils.BuildMir(source);
        var jitCompiler = new MirClrJitCompiler();
        VmClrCompiledProgram jitProgram = jitCompiler.Compile(mir);
        var vm = new VirtualMachine();

        VmValue expected = TestUtils.RunVmMirJit(source) switch
        {
            (long value, _) => VmValue.FromLong(value),
            _ => throw new InvalidOperationException("expected integer result")
        };
        VmValue actual = jitProgram.Execute(
            runtime: vm,
            entryFunctionName: "main");

        Assert.Equal(
            expected: expected.Kind,
            actual: actual.Kind);
        Assert.Equal(
            expected: expected.Payload,
            actual: actual.Payload);
    }

    [Fact]
    public void JitProgram_MatchesVm_ForArraysBuiltinsAndStdout()
    {
        const string source = """
                              fn main() {
                                  var a = array(4, 3);
                                  a[1] = 7;
                                  assert(len(a) == 4);
                                  print(a[0], a[1], len(a));
                                  return a[1];
                              }
                              """;

        (object? vmResult, string vmStdout) = TestUtils.RunVmMirJit(source);

        MirModule mir = TestUtils.BuildMir(source);
        var jitCompiler = new MirClrJitCompiler();
        VmClrCompiledProgram jitProgram = jitCompiler.Compile(mir);
        var runtime = new VirtualMachine();
        var output = new StringWriter();

        using IDisposable outputOverride = BuiltinsCore.PushWriter(output);
        VmValue jitResult = jitProgram.Execute(
            runtime: runtime,
            entryFunctionName: "main");

        Assert.Equal(
            expected: vmResult,
            actual: runtime.ExportValue(jitResult));
        Assert.Equal(
            expected: vmStdout,
            actual: output
                .ToString()
                .TrimEnd('\r', '\n'));
    }

    [Fact]
    public void JitProgram_LeavesNoDanglingCompiledFrames_AfterException()
    {
        const string failingSource = """
                                     fn main() {
                                         assert(0, "boom");
                                         return 1;
                                     }
                                     """;

        const string followUpSource = """
                                      fn main() {
                                          return 42;
                                      }
                                      """;

        MirModule failingMir = TestUtils.BuildMir(failingSource);
        var jitCompiler = new MirClrJitCompiler();
        VmClrCompiledProgram jitProgram = jitCompiler.Compile(failingMir);
        var runtime = new VirtualMachine();

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
        {
            jitProgram.Execute(
                runtime: runtime,
                entryFunctionName: "main");
        });

        Assert.Contains(
            expectedSubstring: "boom",
            actualString: exception.Message,
            comparisonType: StringComparison.OrdinalIgnoreCase);

        MirModule followUpMir = TestUtils.BuildMir(followUpSource);
        VmValue result = jitCompiler.Compile(followUpMir)
            .Execute(
                runtime: runtime,
                entryFunctionName: "main");

        Assert.Equal(
            expected: VmValueKind.I64,
            actual: result.Kind);
        Assert.Equal(
            expected: 42,
            actual: result.AsInt64());
    }
}
