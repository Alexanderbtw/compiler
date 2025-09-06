namespace Compiler.Frontend.Translation.CLI;

public readonly record struct CliArgs(
    bool Verbose,
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
        string? file = args.FirstOrDefault(a => !a.StartsWith("-"));
        string path = string.IsNullOrWhiteSpace(file)
            ? defaultPath
            : file!;

        return new CliArgs(
            Verbose: verbose,
            Path: path);
    }

    private static void PrintUsage()
    {
        string exe = AppDomain.CurrentDomain.FriendlyName;
        Console.WriteLine($"Usage: {exe} [options] [file]\n");
        Console.WriteLine("Options:");
        Console.WriteLine("  -h, --help            Show this help and exit");
        Console.WriteLine("  -v, --verbose         Verbose logs (parse, return value)");
        Console.WriteLine();
        Console.WriteLine("VM options (Compiler.Backend.VM):");
        Console.WriteLine("  --vm-gc-threshold=N   Initial VM heap collection threshold (objects)");
        Console.WriteLine("  --vm-gc-growth=X      VM threshold growth factor after GC (e.g., 1.5)");
        Console.WriteLine("  --vm-gc-auto=on|off   Enable/disable opportunistic collections");
    }
}
