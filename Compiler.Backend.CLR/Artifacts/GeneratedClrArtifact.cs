namespace Compiler.Backend.CLR.Artifacts;

/// <summary>
///     Describes a generated CLR artifact and its on-disk layout.
/// </summary>
public sealed class GeneratedClrArtifact(
    string projectDirectory,
    string projectFilePath,
    string assemblyPath,
    string depsFilePath,
    string runtimeConfigPath)
{
    /// <summary>
    ///     Built assembly path.
    /// </summary>
    public string AssemblyPath { get; } = assemblyPath;

    /// <summary>
    ///     Generated deps.json path.
    /// </summary>
    public string DepsFilePath { get; } = depsFilePath;

    /// <summary>
    ///     Generated project directory.
    /// </summary>
    public string ProjectDirectory { get; } = projectDirectory;

    /// <summary>
    ///     Generated project file path.
    /// </summary>
    public string ProjectFilePath { get; } = projectFilePath;

    /// <summary>
    ///     Generated runtimeconfig.json path.
    /// </summary>
    public string RuntimeConfigPath { get; } = runtimeConfigPath;
}
