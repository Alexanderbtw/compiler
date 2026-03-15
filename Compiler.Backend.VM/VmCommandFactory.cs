using System.CommandLine;

using Compiler.Frontend.Translation.MIR.Common;
using Compiler.Tooling.Options;

using Microsoft.Extensions.Options;

namespace Compiler.Backend.VM;

public sealed class VmCommandFactory(
    IVmRunner runner,
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
            Description = "Print MIR and return value."
        };

        var quietOption = new Option<bool>(
            name: "--quiet",
            "-q")
        {
            Description = "Suppress builtin output."
        };

        var timeOption = new Option<bool>(
            name: "--time",
            "-t")
        {
            Description = "Print execution time."
        };

        var optimizationOption = new Option<string>(
            name: "--opt",
            aliases: ["-O"])
        {
            Description = "MIR optimization level: o0 or o1.",
            DefaultValueFactory = _ => "o1"
        };

        optimizationOption.AcceptOnlyFromAmong(
            "o0",
            "o1");

        var thresholdOption = new Option<int>(name: "--gc-threshold")
        {
            Description = "Initial GC collection threshold.",
            DefaultValueFactory = _ => gcDefaults.Value.InitialThreshold
        };

        var growthOption = new Option<double>(name: "--gc-growth")
        {
            Description = "GC growth factor.",
            DefaultValueFactory = _ => gcDefaults.Value.GrowthFactor
        };

        var autoOption = new Option<string>(name: "--gc-auto")
        {
            Description = "GC auto mode: on/off.",
            DefaultValueFactory = _ => gcDefaults.Value.AutoCollect
                ? "on"
                : "off"
        };

        var statsOption = new Option<bool>(name: "--gc-stats")
        {
            Description = "Print GC stats after execution."
        };

        var runCommand = new Command(
            name: "run",
            description: "Execute MiniLang through the register VM backend.");

        runCommand.Add(fileOption);
        runCommand.Add(verboseOption);
        runCommand.Add(quietOption);
        runCommand.Add(timeOption);
        runCommand.Add(optimizationOption);
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
            string optimizationMode = parseResult.GetValue(optimizationOption) ?? "o1";

            var runOptions = new RunCommandOptions
            {
                Path = file?.FullName ?? defaults.Value.Path,
                Verbose = parseResult.GetValue(verboseOption),
                Quiet = parseResult.GetValue(quietOption),
                Time = parseResult.GetValue(timeOption),
                OptimizationLevel = optimizationMode.Equals(
                    value: "o0",
                    comparisonType: StringComparison.OrdinalIgnoreCase)
                    ? MirOptimizationLevel.O0
                    : MirOptimizationLevel.O1
            };

            var gcOptions = new GcCommandOptions
            {
                InitialThreshold = parseResult.GetValue(thresholdOption),
                GrowthFactor = parseResult.GetValue(growthOption),
                AutoCollect = !string.Equals(
                    a: autoMode,
                    b: "off",
                    comparisonType: StringComparison.OrdinalIgnoreCase),
                PrintStats = parseResult.GetValue(statsOption)
            };

            return await runner.RunAsync(
                options: runOptions,
                gcOptions: gcOptions,
                cancellationToken: cancellationToken);
        });

        var rootCommand = new RootCommand("MiniLang register VM backend host.");
        rootCommand.Add(runCommand);

        return rootCommand;
    }
}
