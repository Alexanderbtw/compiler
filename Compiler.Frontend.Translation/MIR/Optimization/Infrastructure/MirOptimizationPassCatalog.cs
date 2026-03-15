using Compiler.Frontend.Translation.MIR.Common;
using Compiler.Frontend.Translation.MIR.Optimization.Passes;

namespace Compiler.Frontend.Translation.MIR.Optimization.Infrastructure;

public sealed record MirOptimizationPassRegistration(
    MirOptimizationPasses Pass,
    string Name,
    Func<IMirOptimizationPass> Factory);

public static class MirOptimizationPassCatalog
{
    public static string[] Names => Ordered
        .Select(registration => registration.Name)
        .ToArray();

    public static IReadOnlyList<MirOptimizationPassRegistration> Ordered { get; } =
    [
        new MirOptimizationPassRegistration(
            Pass: MirOptimizationPasses.LocalConstantFolding,
            Name: "local-constant-folding",
            Factory: static () => new LocalConstantFoldingPass()),
        new MirOptimizationPassRegistration(
            Pass: MirOptimizationPasses.AlgebraicSimplification,
            Name: "algebraic-simplification",
            Factory: static () => new AlgebraicSimplificationPass()),
        new MirOptimizationPassRegistration(
            Pass: MirOptimizationPasses.PeepholeOptimization,
            Name: "peephole-optimization",
            Factory: static () => new PeepholeOptimizationPass()),
        new MirOptimizationPassRegistration(
            Pass: MirOptimizationPasses.LocalCopyPropagation,
            Name: "local-copy-propagation",
            Factory: static () => new LocalCopyPropagationPass()),
        new MirOptimizationPassRegistration(
            Pass: MirOptimizationPasses.GlobalConstantPropagation,
            Name: "global-constant-propagation",
            Factory: static () => new GlobalConstantPropagationPass()),
        new MirOptimizationPassRegistration(
            Pass: MirOptimizationPasses.BranchFolding,
            Name: "branch-folding",
            Factory: static () => new BranchFoldingPass()),
        new MirOptimizationPassRegistration(
            Pass: MirOptimizationPasses.GlobalCommonSubexpressionElimination,
            Name: "global-common-subexpression-elimination",
            Factory: static () => new GlobalCommonSubexpressionEliminationPass()),
        new MirOptimizationPassRegistration(
            Pass: MirOptimizationPasses.DeadCodeElimination,
            Name: "dead-code-elimination",
            Factory: static () => new DeadCodeEliminationPass()),
        new MirOptimizationPassRegistration(
            Pass: MirOptimizationPasses.UnreachableBlockElimination,
            Name: "unreachable-block-elimination",
            Factory: static () => new UnreachableBlockEliminationPass()),
        new MirOptimizationPassRegistration(
            Pass: MirOptimizationPasses.ControlFlowCleanup,
            Name: "control-flow-cleanup",
            Factory: static () => new ControlFlowCleanupPass())
    ];

    public static IReadOnlyList<string> GetEnabledNames(
        MirOptimizationPasses mask)
    {
        return Ordered
            .Where(registration => mask.HasFlag(registration.Pass))
            .Select(registration => registration.Name)
            .ToArray();
    }

    public static IEnumerable<MirOptimizationPassRegistration> GetEnabledPassesInOrder(
        MirOptimizationPasses mask)
    {
        return Ordered.Where(registration => mask.HasFlag(registration.Pass));
    }

    public static bool TryParse(
        string name,
        out MirOptimizationPasses pass)
    {
        return NameMap.TryGetValue(
            key: name,
            value: out pass);
    }

    private static readonly IReadOnlyDictionary<string, MirOptimizationPasses> NameMap = Ordered.ToDictionary(
        keySelector: registration => registration.Name,
        elementSelector: registration => registration.Pass,
        comparer: StringComparer.OrdinalIgnoreCase);
}
