using Compiler.Tests.HIR;
using Compiler.Translation.HIR.Common;
using Compiler.Translation.MIR;
using Compiler.Translation.MIR.Common;
using Compiler.Translation.MIR.Instructions;
using Compiler.Translation.MIR.Instructions.Abstractions;
using Compiler.Translation.MIR.Operands;

namespace Compiler.Tests.MIR;

public class HirToMirTests
{
    private static MirFunction LowerFn(string src, string name = "main")
    {
        ProgramHir hir = HirAssert.Hir(src);
        MirModule mir = new HirToMir().Lower(hir);
        return mir.Functions.Single(f => f.Name == name);
    }

    private static IEnumerable<MirInstr> AllInstructions(MirFunction f)
    {
        foreach (var b in f.Blocks)
        {
            foreach (var i in b.Instructions) yield return i;
            if (b.Terminator is not null) yield return b.Terminator;
        }
    }

    [Fact]
    public void EmptyProgram_MirHasEntryAndRet()
    {
        var f = LowerFn("fn main() {}");
        Assert.True(f.Blocks.Count >= 1);
        Assert.Contains(f.Blocks, b => b.Terminator is Ret { Value: null });
    }

    [Fact]
    public void VarInit_Addition_LowersToBinThenMove()
    {
        var f = LowerFn("fn main(){ var x = 1 + 2; }");
        var ins = AllInstructions(f).ToList();
        Assert.Contains(ins, i => i is Bin { Op: MBinOp.Add, L: Const { Value: 1L }, R: Const { Value: 2L } });
        Assert.Contains(ins, i => i is Move);
    }

    [Fact]
    public void IfElse_ProducesBrCond_ThenElse_Join()
    {
        var src = @"fn main(){ var x=0; if (1 < 2) { x = 1; } else { x = 2; } }";
        var f = LowerFn(src);
        // Должен быть хотя бы один BrCond
        var cond = AllInstructions(f).OfType<BrCond>().FirstOrDefault();
        Assert.NotNull(cond);
        // И хотя бы один безусловный переход (в join)
        Assert.Contains(f.Blocks, b => b.Terminator is Br);
        // В ветвях должны быть присваивания (Move)
        Assert.Contains(f.Blocks, b => b.Instructions.Any(i => i is Move));
    }

    [Fact]
    public void While_LoopShape_WithBackEdge()
    {
        var src = @"fn main(){ var i=0; while (i < 3) { i = i + 1; } }";
        var f = LowerFn(src);
        // Найдём head с BrCond
        var heads = f.Blocks.Where(b => b.Terminator is BrCond).ToList();
        Assert.NotEmpty(heads);
        var head = heads[0];
        var brc = (BrCond)head.Terminator!;
        var body = brc.IfTrue; // по нашему лоуверингу true → body
        // В теле должен быть безусловный прыжок назад в head (backedge)
        Assert.IsType<Br>(body.Terminator);
        var back = (Br)body.Terminator!;
        Assert.Same(head, back.Target);
    }

    [Fact]
    public void ShortCircuit_And_Or_GenerateConstWrites()
    {
        var src = @"fn main(){ var a=0; var b=0; var x = (a < 1) && (b < 1); var y = (a < 1) || (b < 1); }";
        var f = LowerFn(src);
        var movesOfConst = AllInstructions(f).OfType<Move>().Where(m => m.Src is Const { Value: bool }).ToList();
        // Должны быть записи констант true/false в результирующие временные
        Assert.Contains(movesOfConst, m => m.Src is Const { Value: false });
        Assert.Contains(movesOfConst, m => m.Src is Const { Value: true });
    }

    [Fact]
    public void Calls_UserAndBuiltin()
    {
        var src = "fn id(x){ return x; } fn main(){ var t = id(5); print(\"ok\"); }";
        var f = LowerFn(src);
        var calls = AllInstructions(f).OfType<Call>().ToList();
        Assert.Contains(calls, c => c.Callee == "id");
        Assert.Contains(calls, c => c.Callee == "print");
    }

    [Fact]
    public void Index_LoadAndStore()
    {
        var src = @"fn main(){ var a = array(3); a[0] = 42; var v = a[0]; }";
        var f = LowerFn(src);
        var ins = AllInstructions(f).ToList();
        Assert.Contains(ins, i => i is StoreIndex);
        Assert.Contains(ins, i => i is LoadIndex);
    }

    [Fact]
    public void Return_TerminatesWithRet()
    {
        var src = @"fn main(){ return 123; }";
        var f = LowerFn(src);
        Assert.Contains(f.Blocks, b => b.Terminator is Ret { Value: Const { Value: 123L } });
    }
}

