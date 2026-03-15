using Compiler.Frontend.Translation.MIR.Common;

namespace Compiler.Backend.CLR.Artifacts;

/// <summary>
///     Creates persisted CLR-native assemblies from MIR.
/// </summary>
public interface IDirectClrAssemblyCompiler
{
    /// <summary>
    ///     Builds a CLR artifact on disk.
    /// </summary>
    /// <param name="mir">Source MIR module.</param>
    /// <param name="options">Artifact options.</param>
    /// <returns>Build artifact descriptor.</returns>
    GeneratedClrArtifact Compile(
        MirModule mir,
        DirectClrArtifactOptions options);
}
