namespace Compiler.Translation.Metadata;

public enum BuiltinLoweringKind
{
    CallRuntime, // обычный вызов runtime-метода (например, Console.WriteLine)
    IntrinsicLen, // len(x) → MIR ldlen / аналог
    IntrinsicOrd, // ord(c) → conv.i4

    IntrinsicChr // chr(i) → conv.u2 + проверка диапазона (если нужна)
}