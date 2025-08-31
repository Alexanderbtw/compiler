using Compiler.Frontend.Translation.MIR.Common;
using Compiler.Frontend.Translation.MIR.Instructions;
using Compiler.Frontend.Translation.MIR.Instructions.Abstractions;
using Compiler.Frontend.Translation.MIR.Operands;

namespace Compiler.Tests.MIR;

public class HirToMirTests
{
    [Fact]
    public void Calls_UserAndBuiltin()
    {
        MirFunction f = TestUtils.LowerFunction("fn id(x){ return x; } fn main(){ var t = id(5); print(\"ok\"); }");
        List<Call> calls = TestUtils
            .AllMirInstructions(f)
            .OfType<Call>()
            .ToList();

        Assert.Contains(
            collection: calls,
            filter: c => c.Callee == "id");

        Assert.Contains(
            collection: calls,
            filter: c => c.Callee == "print");
    }
    [Fact]
    public void EmptyProgram_MirHasEntryAndRet()
    {
        MirFunction f = TestUtils.LowerFunction("fn main() {}");
        Assert.True(f.Blocks.Count >= 1);
        Assert.Contains(
            collection: f.Blocks,
            filter: b => b.Terminator is Ret { Value: null });
    }

    [Fact]
    public void IfElse_ProducesBrCond_ThenElse_Join()
    {
        MirFunction f = TestUtils.LowerFunction("fn main(){ var x=0; if (1 < 2) { x = 1; } else { x = 2; } }");
        Assert.NotNull(
            TestUtils
                .AllMirInstructions(f)
                .OfType<BrCond>()
                .FirstOrDefault());

        Assert.Contains(
            collection: f.Blocks,
            filter: b => b.Terminator is Br);

        Assert.Contains(
            collection: f.Blocks,
            filter: b => b.Instructions.Any(i => i is Move));
    }

    [Fact]
    public void Index_LoadAndStore()
    {
        MirFunction f = TestUtils.LowerFunction("fn main(){ var a = array(3); a[0] = 42; var v = a[0]; }");
        List<MirInstr> ins = TestUtils
            .AllMirInstructions(f)
            .ToList();

        Assert.Contains(
            collection: ins,
            filter: i => i is StoreIndex);

        Assert.Contains(
            collection: ins,
            filter: i => i is LoadIndex);
    }

    [Fact]
    public void Return_TerminatesWithRet()
    {
        MirFunction f = TestUtils.LowerFunction("fn main(){ return 123; }");
        Assert.Contains(
            collection: f.Blocks,
            filter: b => b.Terminator is Ret { Value: Const { Value: 123L } });
    }

    [Fact]
    public void ShortCircuit_And_Or_GenerateConstWrites()
    {
        MirFunction f = TestUtils.LowerFunction("fn main(){ var a=0; var b=0; var x = (a < 1) && (b < 1); var y = (a < 1) || (b < 1); }");
        List<Move> moves = TestUtils
            .AllMirInstructions(f)
            .OfType<Move>()
            .Where(m => m.Src is Const { Value: bool })
            .ToList();

        Assert.Contains(
            collection: moves,
            filter: m => m.Src is Const { Value: false });

        Assert.Contains(
            collection: moves,
            filter: m => m.Src is Const { Value: true });
    }

    [Fact]
    public void VarInit_Addition_LowersToBinThenMove()
    {
        MirFunction f = TestUtils.LowerFunction("fn main(){ var x = 1 + 2; }");
        List<MirInstr> ins = TestUtils
            .AllMirInstructions(f)
            .ToList();

        Assert.Contains(
            collection: ins,
            filter: i => i is Bin { Op: MBinOp.Add, L: Const { Value: 1L }, R: Const { Value: 2L } });

        Assert.Contains(
            collection: ins,
            filter: i => i is Move);
    }

    [Fact]
    public void While_LoopShape_WithBackEdge()
    {
        MirFunction f = TestUtils.LowerFunction("fn main(){ var i=0; while (i < 3) { i = i + 1; } }");
        MirBlock head = f.Blocks.First(b => b.Terminator is BrCond);
        MirBlock body = ((BrCond)head.Terminator!).IfTrue;
        var back = Assert.IsType<Br>(body.Terminator!);
        Assert.Same(
            expected: head,
            actual: back.Target);
    }
}
