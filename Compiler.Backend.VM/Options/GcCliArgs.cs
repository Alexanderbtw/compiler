using System.Globalization;

namespace Compiler.Backend.VM.Options;

/// <summary>
///     GC-related CLI options.
/// </summary>
public readonly record struct GcCliArgs(
    GcOptions Options,
    bool PrintStats)
{
    public static GcCliArgs Parse(
        string[] args)
    {
        bool auto = true;
        int threshold = 1024;
        double growth = 2.0;
        bool stats = args.Any(a => a.Equals(
            value: "--vm-gc-stats",
            comparisonType: StringComparison.OrdinalIgnoreCase));

        foreach (string arg in args)
        {
            if (arg.StartsWith(
                    value: "--vm-gc-threshold=",
                    comparisonType: StringComparison.OrdinalIgnoreCase))
            {
                string raw = arg["--vm-gc-threshold=".Length..];

                if (int.TryParse(
                        s: raw,
                        result: out int thr) && thr > 0)
                {
                    threshold = thr;
                }
            }
            else if (arg.StartsWith(
                         value: "--vm-gc-growth=",
                         comparisonType: StringComparison.OrdinalIgnoreCase))
            {
                string raw = arg["--vm-gc-growth=".Length..];

                if (double.TryParse(
                        s: raw,
                        style: NumberStyles.Float,
                        provider: CultureInfo.InvariantCulture,
                        result: out double g) && g >= 1.0)
                {
                    growth = g;
                }
            }
            else if (arg.StartsWith(
                         value: "--vm-gc-auto",
                         comparisonType: StringComparison.OrdinalIgnoreCase))
            {
                string raw = arg["--vm-gc-auto=".Length..]
                    .ToLowerInvariant();

                auto = raw is not ("off" or "false" or "0");
            }
        }

        var options = new GcOptions
        {
            AutoCollect = auto,
            InitialThreshold = threshold,
            GrowthFactor = growth
        };

        return new GcCliArgs(
            Options: options,
            PrintStats: stats);
    }
}
