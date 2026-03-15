using Compiler.Backend.VM;
using Compiler.Frontend.Translation.MIR.Common;
using Compiler.Tooling.Options;

using Microsoft.Extensions.Options;

namespace Compiler.Tests.Tooling;

public sealed class VmCommandFactoryTests
{
    [Fact]
    public async Task Run_Binds_All_Gc_And_Common_Options()
    {
        var runner = new FakeVmRunner();
        var factory = new VmCommandFactory(
            runner: runner,
            defaults: Options.Create(new RunCommandOptions()),
            gcDefaults: Options.Create(new GcCommandOptions()));

        int exitCode = await factory
            .Create()
            .Parse(
            [
                "run",
                "--file",
                "program.minl",
                "--verbose",
                "--quiet",
                "--time",
                "--enable-pass",
                "local-constant-folding",
                "--disable-pass",
                "dead-code-elimination",
                "--gc-threshold",
                "64",
                "--gc-growth",
                "1.25",
                "--gc-auto",
                "off",
                "--gc-stats"
            ])
            .InvokeAsync();

        Assert.Equal(
            expected: 0,
            actual: exitCode);

        Assert.NotNull(runner.Options);
        Assert.NotNull(runner.GcOptions);
        Assert.Equal(
            expected: Path.GetFullPath("program.minl"),
            actual: runner.Options!.Path);

        Assert.True(runner.Options.Verbose);
        Assert.True(runner.Options.Quiet);
        Assert.True(runner.Options.Time);
        Assert.Equal(
            expected: MirOptimizationPasses.StableDefault & ~MirOptimizationPasses.DeadCodeElimination,
            actual: runner.Options.EnabledOptimizationPasses);

        Assert.False(runner.GcOptions!.AutoCollect);
        Assert.Equal(
            expected: 64,
            actual: runner.GcOptions.InitialThreshold);

        Assert.Equal(
            expected: 1.25,
            actual: runner.GcOptions.GrowthFactor,
            precision: 3);

        Assert.True(runner.GcOptions.PrintStats);
    }

    [Fact]
    public async Task Run_Fails_For_Unknown_Pass_Name()
    {
        var runner = new FakeVmRunner();
        var factory = new VmCommandFactory(
            runner: runner,
            defaults: Options.Create(new RunCommandOptions()),
            gcDefaults: Options.Create(new GcCommandOptions()));

        int exitCode = await factory
            .Create()
            .Parse(
            [
                "run",
                "--enable-pass",
                "not-a-pass"
            ])
            .InvokeAsync();

        Assert.NotEqual(
            expected: 0,
            actual: exitCode);

        Assert.Null(runner.Options);
    }

    [Fact]
    public async Task Run_Fails_When_Pass_Is_Both_Enabled_And_Disabled()
    {
        var runner = new FakeVmRunner();
        var factory = new VmCommandFactory(
            runner: runner,
            defaults: Options.Create(new RunCommandOptions()),
            gcDefaults: Options.Create(new GcCommandOptions()));

        int exitCode = await factory
            .Create()
            .Parse(
            [
                "run",
                "--enable-pass",
                "local-constant-folding",
                "--disable-pass",
                "local-constant-folding"
            ])
            .InvokeAsync();

        Assert.NotEqual(
            expected: 0,
            actual: exitCode);

        Assert.Null(runner.Options);
    }

    [Fact]
    public async Task Run_Uses_Defaults_When_Flags_Are_Omitted()
    {
        var defaults = new RunCommandOptions { Path = "main.minl" };
        var gcDefaults = new GcCommandOptions
        {
            AutoCollect = true,
            InitialThreshold = 1024,
            GrowthFactor = 2.0
        };

        var runner = new FakeVmRunner();
        var factory = new VmCommandFactory(
            runner: runner,
            defaults: Options.Create(defaults),
            gcDefaults: Options.Create(gcDefaults));

        int exitCode = await factory
            .Create()
            .Parse(["run"])
            .InvokeAsync();

        Assert.Equal(
            expected: 0,
            actual: exitCode);

        Assert.Equal(
            expected: "main.minl",
            actual: runner.Options!.Path);

        Assert.Equal(
            expected: MirOptimizationPasses.StableDefault,
            actual: runner.Options.EnabledOptimizationPasses);

        Assert.True(runner.GcOptions!.AutoCollect);
        Assert.Equal(
            expected: 1024,
            actual: runner.GcOptions.InitialThreshold);

        Assert.Equal(
            expected: 2.0,
            actual: runner.GcOptions.GrowthFactor,
            precision: 3);

        Assert.False(runner.GcOptions.PrintStats);
    }

    private sealed class FakeVmRunner : IVmRunner
    {
        public GcCommandOptions? GcOptions { get; private set; }

        public RunCommandOptions? Options { get; private set; }

        public Task<int> RunAsync(
            RunCommandOptions options,
            GcCommandOptions gcOptions,
            CancellationToken cancellationToken = default)
        {
            Options = options;
            GcOptions = gcOptions;

            return Task.FromResult(0);
        }
    }
}
