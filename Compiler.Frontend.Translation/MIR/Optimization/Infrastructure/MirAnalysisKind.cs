namespace Compiler.Frontend.Translation.MIR.Optimization.Infrastructure;

[Flags]
public enum MirAnalysisKind
{
    None = 0,
    ControlFlowGraph = 1 << 0,
    Reachability = 1 << 1,
    ConstantState = 1 << 2,
    Liveness = 1 << 3,
    All = ControlFlowGraph | Reachability | ConstantState | Liveness
}
