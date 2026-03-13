using System.Xml.Linq;

namespace Compiler.Tests.Architecture;

public sealed class ProjectBoundaryTests
{
    private static readonly string RepositoryRoot = Path.GetFullPath(
        Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            ".."));

    [Theory]
    [InlineData("Compiler.Backend.JIT.Abstractions/Compiler.Backend.JIT.Abstractions.csproj")]
    [InlineData("Compiler.Runtime.VM/Compiler.Runtime.VM.csproj")]
    [InlineData("Compiler.Frontend/Compiler.Frontend.csproj")]
    [InlineData("Compiler.Frontend.Translation/Compiler.Frontend.Translation.csproj")]
    public void Core_Projects_Do_Not_Reference_Tooling(
        string relativeProjectPath)
    {
        IReadOnlyList<string> projectReferences = GetProjectReferences(relativeProjectPath);

        Assert.DoesNotContain(
            collection: projectReferences,
            filter: reference => reference.EndsWith(
                value: "Compiler.Tooling/Compiler.Tooling.csproj",
                comparisonType: StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void JitAbstractions_Does_Not_Reference_Vm_Runtime()
    {
        IReadOnlyList<string> projectReferences = GetProjectReferences("Compiler.Backend.JIT.Abstractions/Compiler.Backend.JIT.Abstractions.csproj");

        Assert.DoesNotContain(
            collection: projectReferences,
            filter: reference => reference.EndsWith(
                value: "Compiler.Runtime.VM/Compiler.Runtime.VM.csproj",
                comparisonType: StringComparison.OrdinalIgnoreCase));
    }

    private static IReadOnlyList<string> GetProjectReferences(
        string relativeProjectPath)
    {
        string projectPath = Path.Combine(
            path1: RepositoryRoot,
            path2: relativeProjectPath);

        XDocument document = XDocument.Load(projectPath);

        return document
            .Descendants()
            .Where(element => element.Name.LocalName == "ProjectReference")
            .Select(element => element.Attribute("Include")
                ?.Value)
            .Where(include => !string.IsNullOrWhiteSpace(include))
            .Select(include => Path.GetFullPath(
                Path.Combine(
                    path1: Path.GetDirectoryName(projectPath)!,
                    path2: include!)))
            .ToArray();
    }
}
