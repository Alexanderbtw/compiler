using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

using Compiler.Backend.VM;
using Compiler.Backend.VM.Execution.GC;
using Compiler.Backend.VM.Options;
using Compiler.Backend.VM.Values;
using Compiler.Frontend.Translation.CLI;
using Compiler.Frontend.Translation.HIR.Common;
using Compiler.Frontend.Translation.MIR.Common;

namespace Compiler.Backend.JIT.Native;

[ExcludeFromCodeCoverage]
public class Program
{
    public static void Main(
        string[] args)
    {
        CliArgs cli = CliArgs.Parse(args);
        (GcOptions gcOptions, bool printStats) = GcCli.ParseFromArgs(args);

        string src;

        try
        {
            src = File.ReadAllText(cli.Path);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"error: failed to read '{cli.Path}': {ex.Message}");

            return;
        }

        ProgramHir hir = FrontendPipeline.BuildHir(
            src: src,
            verbose: cli.Verbose);

        MirModule mir = FrontendPipeline.BuildMir(hir);

        var vm = new VirtualMachine(options: gcOptions);

        var jit = new MirJitNative();

        TextWriter old = Console.Out;

        if (cli.Quiet)
        {
            Console.SetOut(TextWriter.Null);
        }

        var sw = Stopwatch.StartNew();
        Value ret = jit.Execute(
            vm: vm,
            module: mir,
            entry: "main");

        sw.Stop();

        if (cli.Quiet)
        {
            Console.SetOut(old);
        }

        if (cli.Verbose)
        {
            Console.WriteLine($"[ret] {ret}");
        }

        if (cli.Time)
        {
            Console.WriteLine($"[time] {sw.ElapsedMilliseconds} ms");
        }

        if (printStats)
        {
            GcStats s = vm.GetGcStats();
            Console.WriteLine($"[gc] mode=vm auto={(gcOptions.AutoCollect ? "on" : "off")} threshold={s.Threshold} growth={s.GrowthFactor}");
            Console.WriteLine($"[gc] allocations={s.TotalAllocations} collections={s.Collections} live={s.Live} peak_live={s.PeakLive}");
        }
    }
}
