using Compiler.Backend.CLR;
using Compiler.Backend.VM;
using Compiler.Core.Builtins;
using Compiler.Frontend.Translation.MIR.Common;
using Compiler.Runtime.VM;

namespace Compiler.Tests.CLR;

public sealed class VmBytecodeClrJitTests
{
    [Fact]
    public void BytecodeClrJit_ExecutesRecursiveProgram()
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

        VmCompiledProgram bytecodeProgram = BuildBytecodeProgram(source);
        VmClrCompiledProgram jitProgram = new VmClrJitCompiler().Compile(bytecodeProgram.Program);
        var vm = new VirtualMachine();

        VmValue result = jitProgram.Execute(
            runtime: vm,
            entryFunctionName: "main");

        Assert.Equal(
            expected: 40320L,
            actual: result.AsInt64());
    }

    [Fact]
    public void BytecodeClrJit_MatchesVm_ForBuiltinsArraysAndStdout()
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
        VmCompiledProgram bytecodeProgram = BuildBytecodeProgram(source);
        VmClrCompiledProgram jitProgram = new VmClrJitCompiler().Compile(bytecodeProgram.Program);
        var runtime = new VirtualMachine();
        var output = new StringWriter();

        using IDisposable outputOverride = BuiltinsCore.PushWriter(output);
        VmValue result = jitProgram.Execute(
            runtime: runtime,
            entryFunctionName: "main");

        Assert.Equal(
            expected: vmResult,
            actual: runtime.ExportValue(result));
        Assert.Equal(
            expected: vmStdout,
            actual: output
                .ToString()
                .TrimEnd('\r', '\n'));
    }

    private static VmCompiledProgram BuildBytecodeProgram(
        string source)
    {
        MirModule mir = TestUtils.BuildMir(source);
        return new MirBackendCompiler().Compile(mir);
    }
}
