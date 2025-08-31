namespace Compiler.Tests.Interpretation;

public sealed class ExampleProgramsTests
{
    private readonly static string ProgramDir =
        Path.Combine(AppContext.BaseDirectory, "Tasks");

    private static string Load(string fileName) => File.ReadAllText(Path.Combine(ProgramDir, fileName));

    [Fact]
    public void Factorial_FilePipeline_OK_and_Prints3628800()
    {
        string src = Load("factorial_calculation.minl");
        (_, string stdout) = Utils.Run(src);
        Assert.Equal("2432902008176640000", stdout.Trim());
    }

    [Fact]
    public void QuickSort_FilePipeline_OK_and_FirstLastCorrect()
    {
        string src = Load("array_sorting.minl");

        (_, string stdout) = Utils.Run(src);
        string[] lines = stdout.Split(
            Environment.NewLine,
            StringSplitOptions.RemoveEmptyEntries);

        Assert.Equal("0", lines[0]);
        Assert.Equal("9999", lines[1]);
    }

    [Fact]
    public void Sieve_FilePipeline_OK_and_PrimeCountCorrect()
    {
        string src = Load("prime_number_generation.minl");

        (_, string stdout) = Utils.Run(src);
        Assert.Equal("9592", stdout.Trim());
    }
}
