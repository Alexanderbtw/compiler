using Compiler.Frontend.Translation.HIR.Common;
using Compiler.Frontend.Translation.MIR;
using Compiler.Frontend.Translation.MIR.Common;
using Compiler.Frontend.Translation.MIR.Instructions;
using Compiler.Frontend.Translation.MIR.Instructions.Abstractions;

namespace Compiler.Tests.MIR;

public class MirSnapshotTests
{
    [Fact]
    public void Factorial_Mir_HasCoreShape()
    {
        var src = @"
            fn fact(n) {
                if (n <= 1) return 1;
                return n * fact(n - 1);
            }
            fn main() { return fact(5); }";

        ProgramHir hir = TestUtils.BuildHir(src);
        MirModule mir = new HirToMir().Lower(hir);

        MirFunction fact = mir.Functions.Single(f => f.Name == "fact");

        // параметры
        Assert.Single(fact.ParamNames);
        Assert.Single(fact.ParamRegs);

        // должны быть: одно условное ветвление, один возврат в then, умножение и рекурсивный вызов
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
            filter: t => t is BrCond); // условный переход

        Assert.Contains(
            collection: ins,
            filter: i => i is Bin { Op: MBinOp.Mul }); // умножение

        Assert.Contains(
            collection: ins,
            filter: i => i is Call { Callee: "fact" }); // рекурсивный вызов

        Assert.Contains(
            collection: terms,
            filter: t => t is Ret); // хотя бы один возврат
    }
}
