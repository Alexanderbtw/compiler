using System.CommandLine;

using Compiler.Tooling.Options;

using Microsoft.Extensions.Options;

namespace Compiler.Interpreter;

public sealed class InterpreterCommandFactory(
    IInterpreterRunner runner,
    IOptions<RunCommandOptions> defaults)
{
    public RootCommand Create()
    {
        var fileOption = new Option<FileInfo?>(
            name: "--file",
            "-f")
        {
            Description = "Path to the MiniLang source file."
        };

        var verboseOption = new Option<bool>(
            name: "--verbose",
            "-v")
        {
            Description = "Enable verbose compiler diagnostics."
        };

        var quietOption = new Option<bool>("--quiet")
        {
            Description = "Suppress program stdout from builtins like print."
        };

        var timeOption = new Option<bool>("--time")
        {
            Description = "Print total execution time."
        };

        var runCommand = new Command(
            name: "run",
            description: "Execute MiniLang with the tree-walking interpreter.");

        runCommand.Add(fileOption);
        runCommand.Add(verboseOption);
        runCommand.Add(quietOption);
        runCommand.Add(timeOption);

        runCommand.SetAction(async (
            parseResult,
            cancellationToken) =>
        {
            FileInfo? file = parseResult.GetValue(fileOption);

            var options = new RunCommandOptions
            {
                Path = file?.FullName ?? defaults.Value.Path,
                Verbose = parseResult.GetValue(verboseOption),
                Quiet = parseResult.GetValue(quietOption),
                Time = parseResult.GetValue(timeOption)
            };

            return await runner.RunAsync(
                options: options,
                cancellationToken: cancellationToken);
        });

        var rootCommand = new RootCommand("MiniLang interpreter host.");
        rootCommand.Add(runCommand);

        return rootCommand;
    }
}
