using Compiler.Frontend.Translation.CLI;
using Compiler.Frontend.Translation.HIR.Common;
using Compiler.Frontend.Translation.MIR.Common;

namespace Compiler.Backend.CLR;

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

        var backend = new CilBackend();
        object? ret = backend.RunMain(mir);

        if (verbose && ret is not null)
        {
            Console.WriteLine($"[ret] {ret}");
        }
    }
}
