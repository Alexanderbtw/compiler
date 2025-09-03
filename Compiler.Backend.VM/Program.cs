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
        string src = File.ReadAllText(path);

        ProgramHir hir = FrontendPipeline.BuildHir(
            src: src,
            verbose: verbose);

        MirModule mir = FrontendPipeline.BuildMir(hir);

        VmModule vmModule = new MirToBytecode().Lower(mir);
        var vm = new VirtualMachine(vmModule);
        Value ret = vm.Execute();

        if (verbose)
        {
            Console.WriteLine($"[ret] {ret}");
        }
    }
}
