namespace Compiler.Backend.CLR.Artifacts;

/// <summary>
///     Common options for generated CLR artifacts.
/// </summary>
public abstract class ClrArtifactOptions
{
    /// <summary>
    ///     Output assembly name.
    /// </summary>
    public string AssemblyName { get; init; } = "MiniLang.Generated";

    /// <summary>
    ///     Artifact target framework.
    /// </summary>
    public string TargetFramework { get; init; } = "net10.0";

    /// <summary>
    ///     Entry function name in the source module.
    /// </summary>
    public string EntryFunctionName { get; init; } = "main";

    /// <summary>
    ///     Generated project directory.
    /// </summary>
    public string OutputDirectory { get; init; } = Path.Combine(
        Path.GetTempPath(),
        "minilang-artifacts",
        Guid.NewGuid()
            .ToString("N"));

    /// <summary>
    ///     Build configuration passed to <c>dotnet build</c>.
    /// </summary>
    public string Configuration { get; init; } = "Release";
}
