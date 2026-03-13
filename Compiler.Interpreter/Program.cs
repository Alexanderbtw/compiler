using System.CommandLine;

using Compiler.Tooling;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Compiler.Interpreter;

public class Program
{
    public static async Task<int> Main(
        string[] args)
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);
        builder.Services.AddCompilerTooling();
        builder.Services.AddSingleton<IInterpreterRunner, InterpreterRunner>();
        builder.Services.AddSingleton<InterpreterCommandFactory>();

        using IHost host = builder.Build();

        RootCommand rootCommand = host
            .Services
            .GetRequiredService<InterpreterCommandFactory>()
            .Create();

        return await rootCommand
            .Parse(args)
            .InvokeAsync();
    }
}
