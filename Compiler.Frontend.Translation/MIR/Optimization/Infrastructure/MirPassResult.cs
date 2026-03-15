namespace Compiler.Frontend.Translation.MIR.Optimization;

public readonly record struct MirPassResult(
    bool Changed,
    MirAnalysisKind InvalidatedAnalyses)
{
    public static MirPassResult NoChange => new(
        Changed: false,
        InvalidatedAnalyses: MirAnalysisKind.None);

    public static MirPassResult ChangedAnalyses(
        MirAnalysisKind invalidatedAnalyses)
    {
        return new MirPassResult(
            Changed: true,
            InvalidatedAnalyses: invalidatedAnalyses);
    }
}
