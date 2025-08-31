namespace Compiler.Frontend.Translation.HIR.Stringify;

public readonly record struct SourceSpan(
    int StartLine,
    int StartCol,
    int EndLine,
    int EndCol)
{
    public override string ToString()
    {
        return $"{StartLine}:{StartCol}-{EndLine}:{EndCol}";
    }
}
