using Compiler.Tests.HIR;
using Compiler.Frontend.Translation.HIR.Common;
using Compiler.Frontend.Translation.MIR;
using Compiler.Frontend.Translation.MIR.Common;
using Compiler.Frontend.Translation.MIR.Instructions;
using Compiler.Frontend.Translation.MIR.Instructions.Abstractions;
using Compiler.Frontend.Translation.MIR.Operands;

namespace Compiler.Tests.MIR;

public class HirToMirTests
{
    [Fact]
    public void EmptyProgram_MirHasEntryAndRet()
    {
        var f = TestUtils.LowerFunction("fn main() {}");
        Assert.True(f.Blocks.Count >= 1);
        Assert.Contains(f.Blocks, b => b.Terminator is Ret { Value: null });
    }

    [Fact]
    public void VarInit_Addition_LowersToBinThenMove()
    {
        var f = TestUtils.LowerFunction("fn main(){ var x = 1 + 2; }");
        var ins = TestUtils.AllMirInstructions(f).ToList();
        Assert.Contains(ins, i => i is Bin { Op: MBinOp.Add, L: Const { Value: 1L }, R: Const { Value: 2L } });
        Assert.Contains(ins, i => i is Move);
    }

    [Fact]
    public void IfElse_ProducesBrCond_ThenElse_Join()
    {
        var f = TestUtils.LowerFunction("fn main(){ var x=0; if (1 < 2) { x = 1; } else { x = 2; } }");
        Assert.NotNull(TestUtils.AllMirInstructions(f).OfType<BrCond>().FirstOrDefault());
        Assert.Contains(f.Blocks, b => b.Terminator is Br);
        Assert.Contains(f.Blocks, b => b.Instructions.Any(i => i is Move));
    }

    [Fact]
    public void While_LoopShape_WithBackEdge()
    {
        var f = TestUtils.LowerFunction("fn main(){ var i=0; while (i < 3) { i = i + 1; } }");
        var head = f.Blocks.First(b => b.Terminator is BrCond);
        var body = ((BrCond)head.Terminator!).IfTrue;
        var back = Assert.IsType<Br>(body.Terminator!);
        Assert.Same(head, back.Target);
    }

    [Fact]
    public void ShortCircuit_And_Or_GenerateConstWrites()
    {
        var f = TestUtils.LowerFunction("fn main(){ var a=0; var b=0; var x = (a < 1) && (b < 1); var y = (a < 1) || (b < 1); }");
        var moves = TestUtils.AllMirInstructions(f).OfType<Move>().Where(m => m.Src is Const { Value: bool }).ToList();
        Assert.Contains(moves, m => m.Src is Const { Value: false });
        Assert.Contains(moves, m => m.Src is Const { Value: true });
    }

    [Fact]
    public void Calls_UserAndBuiltin()
    {
        var f = TestUtils.LowerFunction("fn id(x){ return x; } fn main(){ var t = id(5); print(\"ok\"); }");
        var calls = TestUtils.AllMirInstructions(f).OfType<Call>().ToList();
        Assert.Contains(calls, c => c.Callee == "id");
        Assert.Contains(calls, c => c.Callee == "print");
    }

    [Fact]
    public void Index_LoadAndStore()
    {
        var f = TestUtils.LowerFunction("fn main(){ var a = array(3); a[0] = 42; var v = a[0]; }");
        var ins = TestUtils.AllMirInstructions(f).ToList();
        Assert.Contains(ins, i => i is StoreIndex);
        Assert.Contains(ins, i => i is LoadIndex);
    }

    [Fact]
    public void Return_TerminatesWithRet()
    {
        var f = TestUtils.LowerFunction("fn main(){ return 123; }");
        Assert.Contains(f.Blocks, b => b.Terminator is Ret { Value: Const { Value: 123L } });
    }
}

