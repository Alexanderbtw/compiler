using Compiler.Tooling.Options;

using Microsoft.Extensions.DependencyInjection;

namespace Compiler.Tooling;

public static class CompilerToolingServiceCollectionExtensions
{
    public static IServiceCollection AddCompilerTooling(
        this IServiceCollection services)
    {
        services.AddOptions<RunCommandOptions>();
        services.AddOptions<GcCommandOptions>();

        services.AddSingleton<IFrontendPipeline, FrontendPipeline>();

        return services;
    }
}
