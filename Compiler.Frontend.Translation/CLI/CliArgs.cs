namespace Compiler.Frontend.Translation.CLI;

public readonly record struct CliArgs(
    bool Verbose,
    string Path)
{
    public static CliArgs Parse(
        string[] args,
        string defaultPath = "main.minl")
    {
        bool verbose = args.Any(a => a is "--verbose" or "-v");
        string? file = args.FirstOrDefault(a => !a.StartsWith("-"));
        string path = string.IsNullOrWhiteSpace(file)
            ? defaultPath
            : file!;

        return new CliArgs(
            Verbose: verbose,
            Path: path);
    }
}
