using System.Globalization;

using Compiler.Backend.VM.Execution.GC;
using Compiler.Backend.VM.Options;
using Compiler.Backend.VM.Translation;
using Compiler.Backend.VM.Values;
using Compiler.Frontend.Translation.CLI;
using Compiler.Frontend.Translation.HIR.Common;
using Compiler.Frontend.Translation.MIR.Common;

namespace Compiler.Backend.VM;

public class Program
{
    public static void Main(
        string[] args)
    {
        (bool verbose, string path) = CliArgs.Parse(args);

        string src;

        try
        {
            src = File.ReadAllText(path);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"error: failed to read '{path}': {ex.Message}");

            return;
        }

        ProgramHir hir = FrontendPipeline.BuildHir(
            src: src,
            verbose: verbose);

        MirModule mir = FrontendPipeline.BuildMir(hir);

        VmModule vmModule = new MirToBytecode().Lower(mir);

        // VM GC tuning via CLI
        (GcOptions vmGc, bool printGcStats) = GcCli.ParseFromArgs(args);
        var vm = new VirtualMachine(
            module: vmModule,
            options: vmGc);

        Value ret = vm.Execute();

        if (verbose)
        {
            Console.WriteLine($"[ret] {ret}");
        }

        if (printGcStats)
        {
            GcStats s = vm.GetGcStats();
            Console.WriteLine(
                format: "[gc] mode=vm auto={0} threshold={1} growth={2}",
                arg0: vmGc.AutoCollect
                    ? "on"
                    : "off",
                arg1: s.Threshold,
                arg2: s.GrowthFactor.ToString(CultureInfo.InvariantCulture));

            Console.WriteLine(
                format: "[gc] allocations={0} collections={1} live={2} peak_live={3}",
                s.TotalAllocations,
                s.Collections,
                s.Live,
                s.PeakLive);
        }
    }

    // GC options parsing moved to Options.GcCli for testability
}
