using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Compiler.Tooling.Diagnostics;

public static class CompilerInstrumentation
{
    public static readonly ActivitySource ActivitySource = new ActivitySource("Compiler.Tooling");
    public static readonly Meter Meter = new Meter("Compiler.Tooling");
    public static readonly Histogram<double> CompileDurationMs = Meter.CreateHistogram<double>(name: "compiler.backend.compile.duration.ms");

    public static readonly Histogram<double> ExecutionDurationMs = Meter.CreateHistogram<double>(name: "compiler.execution.duration.ms");

    public static readonly Histogram<double> LoweringDurationMs = Meter.CreateHistogram<double>(name: "compiler.lowering.duration.ms");

    public static readonly Histogram<double> OptimizationDurationMs = Meter.CreateHistogram<double>(name: "compiler.optimization.duration.ms");

    public static readonly Histogram<double> ParseDurationMs = Meter.CreateHistogram<double>(name: "compiler.parse.duration.ms");

    public static readonly Histogram<double> PassDurationMs = Meter.CreateHistogram<double>(name: "compiler.optimization.pass.duration.ms");

    public static readonly Histogram<double> SemanticDurationMs = Meter.CreateHistogram<double>(name: "compiler.semantic.duration.ms");
}
