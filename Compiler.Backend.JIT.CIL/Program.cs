using System.CommandLine;

using Compiler.Tooling;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Compiler.Backend.JIT.CIL;

public class Program
{
    public static async Task<int> Main(
        string[] args)
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);
        builder.Services.AddCompilerTooling();
        builder.Services.AddSingleton<ICilRunner, CilRunner>();
        builder.Services.AddSingleton<CilCommandFactory>();

        using IHost host = builder.Build();

        RootCommand rootCommand = host
            .Services
            .GetRequiredService<CilCommandFactory>()
            .Create();

        return await rootCommand
            .Parse(args)
            .InvokeAsync();
    }
}
