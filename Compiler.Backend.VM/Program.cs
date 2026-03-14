using System.CommandLine;

using Compiler.Tooling;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Compiler.Backend.VM;

public class Program
{
    public static async Task<int> Main(
        string[] args)
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);
        builder.Services.AddCompilerTooling();
        builder.Services.AddSingleton<IVmRunner, VmRunner>();
        builder.Services.AddSingleton<VmCommandFactory>();

        using IHost host = builder.Build();

        RootCommand rootCommand = host
            .Services
            .GetRequiredService<VmCommandFactory>()
            .Create();

        return await rootCommand
            .Parse(args)
            .InvokeAsync();
    }
}
