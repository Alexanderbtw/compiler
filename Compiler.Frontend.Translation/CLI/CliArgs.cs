namespace Compiler.Frontend.Translation.CLI;

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
        if (args.Any(a => a is "--help" or "-h"))
        {
            PrintUsage();
            Environment.Exit(0);
        }

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

    private static void PrintUsage()
    {
        string exe = AppDomain.CurrentDomain.FriendlyName;
        Console.WriteLine($"Usage: {exe} [options] [file]\n");
        Console.WriteLine("Options:");
        Console.WriteLine("  -h, --help            Show this help and exit");
        Console.WriteLine("  -v, --verbose         Verbose logs (parse, return value)");
        Console.WriteLine("      --quiet           Suppress program stdout (builtins like print)");
        Console.WriteLine("      --time            Print total execution time (ms)");

        // VM/GC flags are documented by host; CliArgs is backend-agnostic.
    }
}
