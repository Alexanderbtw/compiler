using Compiler.Frontend.Translation.HIR.Common;
using Compiler.Frontend.Translation.MIR;
using Compiler.Frontend.Translation.MIR.Common;
using Compiler.Frontend.Translation.MIR.Instructions;
using Compiler.Frontend.Translation.MIR.Instructions.Abstractions;
using Compiler.Frontend.Translation.MIR.Optimization;
using Compiler.Frontend.Translation.MIR.Optimization.Analyses;

namespace Compiler.Tests.MIR;

public sealed class MirOptimizationTests
{
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
    public void O0_Returns_Raw_Lowered_Mir()
    {
        const string src = """
                           fn main() {
                               var x = 1 + 2;
                               return x;
                           }
                           """;

        ProgramHir hir = TestUtils.BuildHir(src);
        MirModule lowered = new HirToMir().Lower(hir);
        MirModule o0 = TestUtils.BuildMir(
            src: src,
            level: MirOptimizationLevel.O0);

        Assert.Equal(
            expected: lowered.ToString(),
            actual: o0.ToString());
    }

    [Fact]
    public void O1_Does_Not_Delete_Side_Effecting_Or_Index_Instructions()
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
                level: MirOptimizationLevel.O1)
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
    public void O1_Does_Not_Fold_Chr_Or_Division_By_Zero()
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
                level: MirOptimizationLevel.O1)
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
    public void O1_Folds_Constant_Branch_And_Removes_Unreachable_Blocks()
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

        MirFunction function = TestUtils.LowerFunction(
            src: src,
            name: "main");

        Assert.Contains(
            collection: function.Blocks.Select(block => block.Terminator),
            filter: terminator => terminator is BrCond);

        MirModule optimized = TestUtils.BuildMir(
            src: src,
            level: MirOptimizationLevel.O1);

        MirFunction optimizedFunction = optimized.Functions.Single(f => f.Name == "main");

        Assert.DoesNotContain(
            collection: optimizedFunction.Blocks.Select(block => block.Terminator),
            filter: terminator => terminator is BrCond);

        Assert.Single(optimizedFunction.Blocks);
        Assert.Equal(
            expected: "func main\n%entry:\n  ret 10",
            actual: optimizedFunction.ToString());
    }

    [Fact]
    public void O1_Folds_Foldable_Builtins_And_Eliminates_Dead_Temps()
    {
        const string src = """
                           fn main() {
                               len("abc");
                               var x = ord('A');
                               return x;
                           }
                           """;

        MirFunction function = TestUtils
            .BuildMir(
                src: src,
                level: MirOptimizationLevel.O1)
            .Functions
            .Single(f => f.Name == "main");

        Assert.DoesNotContain(
            collection: TestUtils.AllMirInstructions(function),
            filter: instruction => instruction is Call);

        Assert.Equal(
            expected: "func main\n%entry:\n  ret 65",
            actual: function.ToString());
    }
}
