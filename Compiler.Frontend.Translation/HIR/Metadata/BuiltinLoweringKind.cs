namespace Compiler.Frontend.Translation.HIR.Metadata;

public enum BuiltinLoweringKind
{
    CallRuntime, // ordinary runtime call (e.g., Console.WriteLine)
    IntrinsicLen, // len(x) → MIR ldlen / equivalent
    IntrinsicOrd, // ord(c) → conv.i4

    IntrinsicChr // chr(i) → conv.u2 + range check (if needed)
}
