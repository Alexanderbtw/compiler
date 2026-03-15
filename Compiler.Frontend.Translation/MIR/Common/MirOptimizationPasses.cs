namespace Compiler.Frontend.Translation.MIR.Common;

[Flags]
public enum MirOptimizationPasses
{
    None = 0,
    LocalConstantFolding = 1 << 0,
    AlgebraicSimplification = 1 << 1,
    PeepholeOptimization = 1 << 2,
    LocalCopyPropagation = 1 << 3,
    GlobalConstantPropagation = 1 << 4,
    GlobalCommonSubexpressionElimination = 1 << 5,
    DeadCodeElimination = 1 << 6,
    BranchFolding = 1 << 7,
    UnreachableBlockElimination = 1 << 8,
    ControlFlowCleanup = 1 << 9,

    StableDefault = LocalConstantFolding | AlgebraicSimplification | PeepholeOptimization | LocalCopyPropagation |
        GlobalConstantPropagation | GlobalCommonSubexpressionElimination | DeadCodeElimination |
        BranchFolding | UnreachableBlockElimination | ControlFlowCleanup
}
