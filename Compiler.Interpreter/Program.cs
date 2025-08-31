using System;
using System.IO;

using Compiler.Frontend.Translation.CLI;
using Compiler.Frontend.Translation.HIR.Common;

namespace Compiler.Interpreter;

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

        var interpreter = new Interpreter(hir);
        object? ret = interpreter.Run();

        if (verbose)
        {
            Console.WriteLine($"[ret] {ret ?? "null"}");
        }
    }
}
