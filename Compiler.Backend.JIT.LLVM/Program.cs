using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

using Compiler.Backend.VM;
using Compiler.Backend.VM.Execution.GC;
using Compiler.Backend.VM.Options;
using Compiler.Backend.VM.Values;
using Compiler.Frontend.Translation.CLI;
using Compiler.Frontend.Translation.HIR.Common;
using Compiler.Frontend.Translation.MIR.Common;

using LLVMSharp.Interop;

namespace Compiler.Backend.JIT.LLVM;

[ExcludeFromCodeCoverage]
public class Program
{
    // Note: point DYLD_FALLBACK_LIBRARY_PATH or LIBLLVM_PATH to your LLVM install on macOS.
    public static void Main(
        string[] args)
    {
        CliArgs cliArgs = CliArgs.Parse(args);
        (GcOptions gcOptions, bool printStats) = GcCliArgs.Parse(args);
        LlvmCliArgs llvmCliArgs = LlvmCliArgs.Parse(args);

        string src;

        try { src = File.ReadAllText(cliArgs.Path); }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"error: failed to read '{cliArgs.Path}': {ex.Message}");

            return;
        }

        ProgramHir hir = FrontendPipeline.BuildHir(
            src: src,
            verbose: cliArgs.Verbose);

        MirModule mir = FrontendPipeline.BuildMir(hir);

        var virtualMachine = new VirtualMachine(options: gcOptions);

        var llvmJit = new MirJitLlvm();

        TextWriter originalOut = Console.Out;

        if (cliArgs.Quiet)
        {
            Console.SetOut(TextWriter.Null);
        }

        if (llvmCliArgs.DumpIr)
        {
            var emitter = new LlvmEmitter();
            LLVMModuleRef module = emitter.EmitModule(mir);
            Console.WriteLine(module.PrintToString());
        }

        var stopwatch = Stopwatch.StartNew();

        Value ret = llvmJit.Execute(
            virtualMachine: virtualMachine,
            mirModule: mir,
            entryFunctionName: "main");

        stopwatch.Stop();

        if (cliArgs.Quiet)
        {
            Console.SetOut(originalOut);
        }

        if (cliArgs.Verbose)
        {
            Console.WriteLine($"[ret] {ret}");
        }

        if (cliArgs.Time)
        {
            Console.WriteLine($"[time] {stopwatch.ElapsedMilliseconds} ms");
        }

        if (printStats)
        {
            GcStats stats = virtualMachine.GetGcStats();
            Console.WriteLine($"[gc] mode=vm auto={(gcOptions.AutoCollect ? "on" : "off")} threshold={stats.Threshold} growth={stats.GrowthFactor}");
            Console.WriteLine($"[gc] allocations={stats.TotalAllocations} collections={stats.Collections} live={stats.Live} peak_live={stats.PeakLive}");
        }
    }
}
