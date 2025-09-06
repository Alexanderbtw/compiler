using System.Globalization;

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
        GcOptions vmGc = ParseVmGcOptions(args);
        var vm = new VirtualMachine(
            module: vmModule,
            options: vmGc);

        Value ret = vm.Execute();

        if (verbose)
        {
            Console.WriteLine($"[ret] {ret}");
        }
    }

    private static GcOptions ParseVmGcOptions(
        string[] args)
    {
        bool auto = true;
        int threshold = 1024;
        double growth = 2.0;

        foreach (string arg in args)
        {
            if (arg.StartsWith(
                    value: "--vm-gc-threshold=",
                    comparisonType: StringComparison.OrdinalIgnoreCase))
            {
                string raw = arg["--vm-gc-threshold=".Length..];

                if (int.TryParse(
                        s: raw,
                        result: out int thr) && thr > 0)
                {
                    threshold = thr;
                }
            }
            else if (arg.StartsWith(
                         value: "--vm-gc-growth=",
                         comparisonType: StringComparison.OrdinalIgnoreCase))
            {
                string raw = arg["--vm-gc-growth=".Length..];

                if (double.TryParse(
                        s: raw,
                        style: NumberStyles.Float,
                        provider: CultureInfo.InvariantCulture,
                        result: out double g) && g >= 1.0)
                {
                    growth = g;
                }
            }
            else if (arg.StartsWith(
                         value: "--vm-gc-auto=",
                         comparisonType: StringComparison.OrdinalIgnoreCase))
            {
                string raw = arg["--vm-gc-auto=".Length..]
                    .ToLowerInvariant();

                auto = raw is not ("off" or "false" or "0");
            }
        }

        return new GcOptions
        {
            AutoCollect = auto,
            InitialThreshold = threshold,
            GrowthFactor = growth
        };
    }
}
