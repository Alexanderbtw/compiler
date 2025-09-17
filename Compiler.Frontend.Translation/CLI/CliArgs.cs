namespace Compiler.Frontend.Translation.CLI;

/// <summary>
///     Shared CLI flags used by tools (Interpreter, JIT hosts).
///     Picks the first non-flag arg as the input path.
/// </summary>
public readonly record struct CliArgs(
    bool Verbose,
    bool Quiet,
    bool Time,
    string Path)
{
    public static CliArgs Parse(
        string[] args,
        string defaultPath = "main.minl")
    {
        bool verbose = args.Any(a => a is "--verbose" or "-v");
        bool quiet = args.Any(a => a.Equals(
            value: "--quiet",
            comparisonType: StringComparison.OrdinalIgnoreCase));

        bool time = args.Any(a => a.Equals(
            value: "--time",
            comparisonType: StringComparison.OrdinalIgnoreCase));

        string? file = args.FirstOrDefault(a => !a.StartsWith('-'));
        string path = string.IsNullOrWhiteSpace(file)
            ? defaultPath
            : file!;

        return new CliArgs(
            Verbose: verbose,
            Quiet: quiet,
            Time: time,
            Path: path);
    }
}
