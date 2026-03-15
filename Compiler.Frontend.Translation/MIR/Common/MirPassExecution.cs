namespace Compiler.Frontend.Translation.MIR.Common;

public sealed record MirPassExecution(
    string Name,
    bool Changed,
    double DurationMs,
    int Iteration);
