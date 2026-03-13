using System.Diagnostics.Metrics;

namespace Compiler.Runtime.VM.Execution.Diagnostics;

public static class VmRuntimeInstrumentation
{
    public static readonly Meter Meter = new Meter("Compiler.Runtime.VM");
    public static readonly Counter<long> ArrayAllocations = Meter.CreateCounter<long>(name: "compiler.vm.array.allocations");

    public static readonly Counter<long> Collections = Meter.CreateCounter<long>(name: "compiler.vm.gc.collections");

    public static readonly Counter<long> StringAllocations = Meter.CreateCounter<long>(name: "compiler.vm.string.allocations");
}
