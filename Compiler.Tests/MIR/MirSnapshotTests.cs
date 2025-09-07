using Compiler.Frontend.Translation.HIR.Common;
using Compiler.Frontend.Translation.MIR;
using Compiler.Frontend.Translation.MIR.Common;
using Compiler.Frontend.Translation.MIR.Instructions;
using Compiler.Frontend.Translation.MIR.Instructions.Abstractions;

namespace Compiler.Tests.MIR;

public sealed class MirSnapshotTests
{
    [Fact]
    public void Factorial_Mir_HasCoreShape()
    {
        string src = @"
            fn fact(n) {
                if (n <= 1) return 1;
                return n * fact(n - 1);
            }
            fn main() { return fact(5); }";

        ProgramHir hir = TestUtils.BuildHir(src);
        MirModule mir = new HirToMir().Lower(hir);

        MirFunction fact = mir.Functions.Single(f => f.Name == "fact");

        // Parameters
        Assert.Single(fact.ParamNames);
        Assert.Single(fact.ParamRegs);

        // Should contain: one conditional branch, one return in then, a multiplication and a recursive call
        List<MirInstr> ins = fact
            .Blocks
            .SelectMany(b => b.Instructions)
            .ToList();

        List<MirInstr?> terms = fact
            .Blocks
            .Select(b => b.Terminator)
            .ToList();

        Assert.Contains(
            collection: terms,
            filter: t => t is BrCond); // conditional branch

        Assert.Contains(
            collection: ins,
            filter: i => i is Bin { Op: MBinOp.Mul }); // multiplication

        Assert.Contains(
            collection: ins,
            filter: i => i is Call { Callee: "fact" }); // recursive call

        Assert.Contains(
            collection: terms,
            filter: t => t is Ret); // at least one return
    }
}
