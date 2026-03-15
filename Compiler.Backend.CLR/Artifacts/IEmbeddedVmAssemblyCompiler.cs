using Compiler.Frontend.Translation.MIR.Common;

namespace Compiler.Backend.CLR.Artifacts;

/// <summary>
///     Creates persisted assemblies that embed the VM/runtime implementation.
/// </summary>
public interface IEmbeddedVmAssemblyCompiler
{
    /// <summary>
    ///     Builds an embedded-VM CLR artifact on disk.
    /// </summary>
    /// <param name="mir">Source MIR module.</param>
    /// <param name="options">Artifact options.</param>
    /// <returns>Build artifact descriptor.</returns>
    GeneratedClrArtifact Compile(
        MirModule mir,
        EmbeddedVmArtifactOptions options);
}
