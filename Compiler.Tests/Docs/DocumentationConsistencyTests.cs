namespace Compiler.Tests.Docs;

public sealed class DocumentationConsistencyTests
{
    private static readonly string RepositoryRoot = Path.GetFullPath(
        Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            ".."));

    [Theory]
    [InlineData("README.md")]
    [InlineData("docs.md")]
    public void Documentation_Does_Not_Reference_Removed_Clr_Project(
        string relativePath)
    {
        string text = ReadDocumentation(relativePath);

        Assert.DoesNotContain(
            expectedSubstring: "Compiler.Backend.CLR",
            actualString: text,
            comparisonType: StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("README.md")]
    [InlineData("docs.md")]
    public void Documentation_Reflects_Current_Hosts_And_Tooling(
        string relativePath)
    {
        string text = ReadDocumentation(relativePath);

        Assert.Contains(
            expectedSubstring: "Compiler.Tooling",
            actualString: text,
            comparisonType: StringComparison.Ordinal);

        Assert.Contains(
            expectedSubstring: "Experimental/Typing",
            actualString: text,
            comparisonType: StringComparison.Ordinal);

        Assert.Contains(
            expectedSubstring: "dotnet run --project Compiler.Interpreter -- run --file",
            actualString: text,
            comparisonType: StringComparison.Ordinal);

        Assert.Contains(
            expectedSubstring: "dotnet run --project Compiler.Backend.JIT.CIL -- run --file",
            actualString: text,
            comparisonType: StringComparison.Ordinal);

        Assert.Contains(
            expectedSubstring: "dotnet run --project Compiler.Benchmarks -c Release",
            actualString: text,
            comparisonType: StringComparison.Ordinal);
    }

    private static string ReadDocumentation(
        string relativePath)
    {
        return File.ReadAllText(
            Path.Combine(
                path1: RepositoryRoot,
                path2: relativePath));
    }
}
