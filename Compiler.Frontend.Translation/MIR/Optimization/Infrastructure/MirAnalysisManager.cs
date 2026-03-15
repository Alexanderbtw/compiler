using Compiler.Frontend.Translation.MIR.Common;
using Compiler.Frontend.Translation.MIR.Optimization.Analyses;

namespace Compiler.Frontend.Translation.MIR.Optimization.Infrastructure;

public sealed class MirAnalysisManager(
    MirFunction function)
{
    private ConstantStateAnalysis? _constantState;
    private ControlFlowGraph? _controlFlowGraph;
    private LivenessAnalysis? _liveness;
    private ReachabilityAnalysis? _reachability;

    public MirFunction Function { get; } = function;

    public ConstantStateAnalysis GetConstantStateAnalysis()
    {
        return _constantState ??= new ConstantStateAnalysis(
            function: Function,
            cfg: GetControlFlowGraph());
    }

    public ControlFlowGraph GetControlFlowGraph()
    {
        return _controlFlowGraph ??= new ControlFlowGraph(Function);
    }

    public LivenessAnalysis GetLivenessAnalysis()
    {
        return _liveness ??= new LivenessAnalysis(
            function: Function,
            cfg: GetControlFlowGraph());
    }

    public ReachabilityAnalysis GetReachabilityAnalysis()
    {
        return _reachability ??= new ReachabilityAnalysis(GetControlFlowGraph());
    }

    public void Invalidate(
        MirAnalysisKind kinds)
    {
        if (kinds.HasFlag(MirAnalysisKind.ControlFlowGraph))
        {
            _controlFlowGraph = null;
            _reachability = null;
            _constantState = null;
            _liveness = null;
        }

        if (kinds.HasFlag(MirAnalysisKind.Reachability))
        {
            _reachability = null;
        }

        if (kinds.HasFlag(MirAnalysisKind.ConstantState))
        {
            _constantState = null;
        }

        if (kinds.HasFlag(MirAnalysisKind.Liveness))
        {
            _liveness = null;
        }
    }
}
