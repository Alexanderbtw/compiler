using Compiler.Frontend.Translation.HIR.Common;
using Compiler.Frontend.Translation.MIR;
using Compiler.Frontend.Translation.MIR.Common;
using Compiler.Frontend.Translation.MIR.Instructions;
using Compiler.Frontend.Translation.MIR.Instructions.Abstractions;
using Compiler.Frontend.Translation.MIR.Operands;
using Compiler.Frontend.Translation.MIR.Optimization.Analyses;

namespace Compiler.Tests.MIR;

public sealed class MirOptimizationTests
{
    [Fact]
    public void AlgebraicSimplification_Applies_Safe_Identities()
    {
        const string src = """
                           fn main() {
                               var x = 7;
                               var a = x + 0;
                               var b = a * 1;
                               var c = b % 1;
                               return c;
                           }
                           """;

        MirFunction function = TestUtils
            .BuildMir(
                src: src,
                enabledPasses: MirOptimizationPasses.AlgebraicSimplification)
            .Functions
            .Single(f => f.Name == "main");

        Assert.DoesNotContain(
            collection: TestUtils.AllMirInstructions(function),
            filter: instruction => instruction is Bin { Op: MBinOp.Add or MBinOp.Mul or MBinOp.Mod });
    }

    [Fact]
    public void ControlFlowGraph_Builds_Diamond_Shape()
    {
        const string src = """
                           fn main() {
                               var x = 1;
                               if (x) {
                                   print(1);
                               }
                               else {
                                   print(2);
                               }
                               return 0;
                           }
                           """;

        MirFunction function = TestUtils.LowerFunction(
            src: src,
            name: "main");

        var cfg = new ControlFlowGraph(function);

        MirBlock entry = function.Blocks[0];
        Assert.Equal(
            expected: 2,
            actual: cfg.GetSuccessors(entry)
                .Count);

        MirBlock join = function.Blocks.Single(block => block.Name.StartsWith(
            value: "join_",
            comparisonType: StringComparison.Ordinal));

        Assert.Equal(
            expected: 2,
            actual: cfg.GetPredecessors(join)
                .Count);

        Assert.Equal(
            expected: function.Blocks.Count - 1,
            actual: cfg.ReachableBlocks.Count);
    }

    [Fact]
    public void DeadCodeElimination_Removes_Redundant_Pure_Instructions()
    {
        const string src = """
                           fn main() {
                               var a = 1 + 2;
                               var b = a + 3;
                               return 0;
                           }
                           """;

        MirFunction function = TestUtils
            .BuildMir(
                src: src,
                enabledPasses: MirOptimizationPasses.LocalConstantFolding |
                MirOptimizationPasses.DeadCodeElimination |
                MirOptimizationPasses.UnreachableBlockElimination |
                MirOptimizationPasses.ControlFlowCleanup)
            .Functions
            .Single(f => f.Name == "main");

        Assert.Equal(
            expected: "func main\n%entry:\n  ret 0",
            actual: function.ToString());
    }

    [Fact]
    public void GlobalConstantPropagation_Works_Across_Diamond()
    {
        const string src = """
                           fn main() {
                               var x = 1;
                               if (x == 1) {
                                   x = 5;
                               }
                               else {
                                   x = 5;
                               }
                               return x;
                           }
                           """;

        MirFunction function = TestUtils
            .BuildMir(
                src: src,
                enabledPasses: MirOptimizationPasses.GlobalConstantPropagation | MirOptimizationPasses.BranchFolding)
            .Functions
            .Single(f => f.Name == "main");

        Assert.Contains(
            collection: function.Blocks,
            filter: block => block.Terminator is Ret { Value: Const { Value: 5L } });
    }

    [Fact]
    public void GlobalCse_Eliminates_Repeated_Pure_Expression_After_Join()
    {
        const string src = """
                           fn main() {
                               var a = 1;
                               var b = 2;
                               var x = a + b;
                               if (a < b) {
                                   var t = 0;
                               }
                               else {
                                   var u = 0;
                               }
                               var y = a + b;
                               return y;
                           }
                           """;

        MirFunction function = TestUtils
            .BuildMir(
                src: src,
                enabledPasses: MirOptimizationPasses.GlobalCommonSubexpressionElimination)
            .Functions
            .Single(f => f.Name == "main");

        Assert.Single(
            collection: TestUtils
                .AllMirInstructions(function)
                .OfType<Bin>(),
            predicate: bin => bin.Op == MBinOp.Add);
    }

    [Fact]
    public void GlobalCse_Kills_On_Store_And_Call()
    {
        const string src = """
                           fn main() {
                               var a = 1;
                               var b = 2;
                               var x = a + b;
                               var arr = array(1, 0);
                               arr[0] = 5;
                               print(x);
                               var y = a + b;
                               return y;
                           }
                           """;

        MirFunction function = TestUtils
            .BuildMir(
                src: src,
                enabledPasses: MirOptimizationPasses.GlobalCommonSubexpressionElimination)
            .Functions
            .Single(f => f.Name == "main");

        Assert.Equal(
            expected: 2,
            actual: TestUtils
                .AllMirInstructions(function)
                .OfType<Bin>()
                .Count(bin => bin.Op == MBinOp.Add));
    }

    [Fact]
    public void LocalConstantFolding_Folds_Safe_Literals_And_Builtins()
    {
        const string src = """
                           fn main() {
                               var a = 1 + 2;
                               var b = ord('A');
                               return b;
                           }
                           """;

        MirFunction function = TestUtils
            .BuildMir(
                src: src,
                enabledPasses: MirOptimizationPasses.LocalConstantFolding)
            .Functions
            .Single(f => f.Name == "main");

        Assert.DoesNotContain(
            collection: TestUtils.AllMirInstructions(function),
            filter: instruction => instruction is Bin { Op: MBinOp.Add });

        Assert.DoesNotContain(
            collection: TestUtils.AllMirInstructions(function),
            filter: instruction => instruction is Call { Callee: "ord" });
    }

    [Fact]
    public void LocalCopyPropagation_Rewrites_IntraBlock_Register_Chain()
    {
        const string src = """
                           fn main() {
                               var x = 1;
                               var y = x;
                               var z = y;
                               return z;
                           }
                           """;

        MirFunction function = TestUtils
            .BuildMir(
                src: src,
                enabledPasses: MirOptimizationPasses.LocalCopyPropagation)
            .Functions
            .Single(f => f.Name == "main");

        Assert.Contains(
            collection: TestUtils.AllMirInstructions(function),
            filter: instruction => instruction is Move { Src: Const { Value: 1L } });
    }

    [Fact]
    public void LocalOnly_And_CfgOnly_Pipelines_Are_Independently_Usable()
    {
        const string src = """
                           fn main() {
                               var a = 1 + 2;
                               var b = a;
                               if (b == 3) {
                                   return b;
                               }
                               return 0;
                           }
                           """;

        MirModule localOnly = TestUtils.BuildMir(
            src: src,
            enabledPasses: LocalOnly);

        MirModule cfgOnly = TestUtils.BuildMir(
            src: src,
            enabledPasses: CfgOnly);

        Assert.NotEqual(
            expected: localOnly.ToString(),
            actual: cfgOnly.ToString());
    }

    [Fact]
    public void NoPasses_Return_Raw_Lowered_Mir()
    {
        const string src = """
                           fn main() {
                               var x = 1 + 2;
                               return x;
                           }
                           """;

        ProgramHir hir = TestUtils.BuildHir(src);
        MirModule lowered = new HirToMir().Lower(hir);
        MirModule baseline = TestUtils.BuildMir(
            src: src,
            enabledPasses: MirOptimizationPasses.None);

        Assert.Equal(
            expected: lowered.ToString(),
            actual: baseline.ToString());
    }

    [Fact]
    public void PeepholeOptimization_Rewrites_Move_Then_Ret()
    {
        const string src = """
                           fn main() {
                               var x = 1;
                               return x;
                           }
                           """;

        MirFunction function = TestUtils
            .BuildMir(
                src: src,
                enabledPasses: MirOptimizationPasses.PeepholeOptimization)
            .Functions
            .Single(f => f.Name == "main");

        Assert.Contains(
            collection: function.Blocks,
            filter: block => block.Terminator is Ret { Value: Const { Value: 1L } });
    }

    [Fact]
    public void StableDefault_Cleans_Up_Folded_Branches_And_Blocks()
    {
        const string src = """
                           fn main() {
                               var x = 1;
                               if (x == 1) {
                                   return 10;
                               }
                               else {
                                   return 20;
                               }
                           }
                           """;

        MirFunction optimizedFunction = TestUtils
            .BuildMir(
                src: src,
                enabledPasses: MirOptimizationPasses.StableDefault)
            .Functions
            .Single(f => f.Name == "main");

        Assert.DoesNotContain(
            collection: optimizedFunction.Blocks.Select(block => block.Terminator),
            filter: terminator => terminator is BrCond);

        Assert.Single(optimizedFunction.Blocks);
        Assert.Equal(
            expected: "func main\n%entry:\n  ret 10",
            actual: optimizedFunction.ToString());
    }

    [Fact]
    public void StableDefault_Does_Not_Delete_Side_Effecting_Or_Index_Instructions()
    {
        const string src = """
                           fn main() {
                               var a = array(2, 5);
                               a[0] = 1;
                               a[1];
                               print(a[0]);
                               return 0;
                           }
                           """;

        MirFunction function = TestUtils
            .BuildMir(
                src: src,
                enabledPasses: MirOptimizationPasses.StableDefault)
            .Functions
            .Single(f => f.Name == "main");

        Assert.Contains(
            collection: TestUtils.AllMirInstructions(function),
            filter: instruction => instruction is StoreIndex);

        Assert.Contains(
            collection: TestUtils.AllMirInstructions(function),
            filter: instruction => instruction is LoadIndex);

        Assert.Contains(
            collection: TestUtils.AllMirInstructions(function),
            filter: instruction => instruction is Call { Callee: "print" });
    }

    [Fact]
    public void StableDefault_Does_Not_Fold_Chr_Or_Division_By_Zero()
    {
        const string src = """
                           fn main() {
                               var x = chr(70000);
                               var y = 1 / 0;
                               return x;
                           }
                           """;

        MirFunction function = TestUtils
            .BuildMir(
                src: src,
                enabledPasses: MirOptimizationPasses.StableDefault)
            .Functions
            .Single(f => f.Name == "main");

        Assert.Contains(
            collection: TestUtils.AllMirInstructions(function),
            filter: instruction => instruction is Call { Callee: "chr" });

        Assert.Contains(
            collection: TestUtils.AllMirInstructions(function),
            filter: instruction => instruction is Bin { Op: MBinOp.Div });
    }

    [Fact]
    public void StableDefault_Preserves_Runtime_Semantics()
    {
        const string src = """
                           fn main() {
                               var x = 1 + 2;
                               if (x == 3) {
                                   print("ok");
                               }
                               return x;
                           }
                           """;

        (object? retBaseline, string outBaseline) = TestUtils.RunVmMirJit(
            src: src,
            enabledPasses: MirOptimizationPasses.None);

        (object? retOptimized, string outOptimized) = TestUtils.RunVmMirJit(
            src: src,
            enabledPasses: MirOptimizationPasses.StableDefault);

        TestUtils.AssertProgramResult(
            expectedRet: retBaseline,
            expectedStdout: outBaseline,
            actualRet: retOptimized,
            actualStdout: outOptimized);
    }

    private const MirOptimizationPasses CfgOnly = MirOptimizationPasses.GlobalConstantPropagation |
        MirOptimizationPasses.GlobalCommonSubexpressionElimination |
        MirOptimizationPasses.DeadCodeElimination |
        MirOptimizationPasses.BranchFolding |
        MirOptimizationPasses.UnreachableBlockElimination |
        MirOptimizationPasses.ControlFlowCleanup;

    private const MirOptimizationPasses LocalOnly = MirOptimizationPasses.LocalConstantFolding |
        MirOptimizationPasses.AlgebraicSimplification |
        MirOptimizationPasses.PeepholeOptimization |
        MirOptimizationPasses.LocalCopyPropagation;
}
