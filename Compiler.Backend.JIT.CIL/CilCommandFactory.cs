using System.CommandLine;

using Compiler.Tooling.Options;

using Microsoft.Extensions.Options;

namespace Compiler.Backend.JIT.CIL;

public sealed class CilCommandFactory(
    ICilRunner runner,
    IOptions<RunCommandOptions> defaults,
    IOptions<GcCommandOptions> gcDefaults)
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

        var thresholdOption = new Option<int>("--vm-gc-threshold")
        {
            Description = "Initial VM heap collection threshold (objects).",
            DefaultValueFactory = _ => gcDefaults.Value.InitialThreshold
        };

        var growthOption = new Option<double>("--vm-gc-growth")
        {
            Description = "GC threshold growth factor.",
            DefaultValueFactory = _ => gcDefaults.Value.GrowthFactor
        };

        var autoOption = new Option<string>("--vm-gc-auto")
        {
            Description = "Enable or disable opportunistic collections: on|off.",
            DefaultValueFactory = _ => gcDefaults.Value.AutoCollect
                ? "on"
                : "off"
        };

        autoOption.AcceptOnlyFromAmong(
            "on",
            "off",
            "true",
            "false",
            "1",
            "0");

        var statsOption = new Option<bool>("--vm-gc-stats")
        {
            Description = "Print VM GC statistics after execution."
        };

        var runCommand = new Command(
            name: "run",
            description: "Execute MiniLang through the CIL backend.");

        runCommand.Add(fileOption);
        runCommand.Add(verboseOption);
        runCommand.Add(quietOption);
        runCommand.Add(timeOption);
        runCommand.Add(thresholdOption);
        runCommand.Add(growthOption);
        runCommand.Add(autoOption);
        runCommand.Add(statsOption);

        runCommand.SetAction(async (
            parseResult,
            cancellationToken) =>
        {
            FileInfo? file = parseResult.GetValue(fileOption);
            string autoMode = parseResult.GetValue(autoOption) ?? "on";

            var options = new RunCommandOptions
            {
                Path = file?.FullName ?? defaults.Value.Path,
                Verbose = parseResult.GetValue(verboseOption),
                Quiet = parseResult.GetValue(quietOption),
                Time = parseResult.GetValue(timeOption)
            };

            var gcOptions = new GcCommandOptions
            {
                InitialThreshold = parseResult.GetValue(thresholdOption),
                GrowthFactor = parseResult.GetValue(growthOption),
                AutoCollect = ParseAutoCollect(autoMode),
                PrintStats = parseResult.GetValue(statsOption)
            };

            return await runner.RunAsync(
                options: options,
                gcOptions: gcOptions,
                cancellationToken: cancellationToken);
        });

        var rootCommand = new RootCommand("MiniLang CIL backend host.");
        rootCommand.Add(runCommand);

        return rootCommand;
    }

    private static bool ParseAutoCollect(
        string value)
    {
        return value.ToLowerInvariant() switch
        {
            "off" or "false" or "0" => false,
            "on" or "true" or "1" => true,
            _ => throw new ArgumentException(
                message: "Unsupported value for --vm-gc-auto. Use on|off|true|false|1|0.",
                paramName: nameof(value))
        };
    }
}
