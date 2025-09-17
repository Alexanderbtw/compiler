namespace Compiler.Backend.JIT.LLVM;

/// <summary>
///     CLI flags for LLVM JIT.
/// </summary>
public readonly record struct LlvmCliArgs(
    bool DumpIr)
{
    public static LlvmCliArgs Parse(
        string[] args)
    {
        bool dumpIr = args.Any(a => a is "--dump-ir");

        return new LlvmCliArgs(DumpIr: dumpIr);
    }
}
