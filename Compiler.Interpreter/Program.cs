using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;

using Compiler.Frontend.Translation.CLI;
using Compiler.Frontend.Translation.HIR.Common;

namespace Compiler.Interpreter;

[ExcludeFromCodeCoverage]
public class Program
{
    public static void Main(
        string[] args)
    {
        CliArgs cliArgs = CliArgs.Parse(args);

        string src;

        try
        {
            src = File.ReadAllText(cliArgs.Path);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"error: failed to read '{cliArgs.Path}': {ex.Message}");

            return;
        }

        ProgramHir hir = FrontendPipeline.BuildHir(
            src: src,
            verbose: cliArgs.Verbose);

        var interpreter = new Interpreter(hir);
        object? ret = interpreter.Run();

        if (cliArgs.Verbose)
        {
            Console.WriteLine($"[ret] {ret ?? "null"}");
        }
    }
}
