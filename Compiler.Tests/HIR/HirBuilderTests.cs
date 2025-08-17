using Compiler.Translation.HIR.Common;
using Compiler.Translation.HIR.Expressions;
using Compiler.Translation.HIR.Expressions.Abstractions;
using Compiler.Translation.HIR.Statements;
using Compiler.Translation.HIR.Statements.Abstractions;

namespace Compiler.Tests.HIR;

public class HirBuilderTests
{
    [Fact]
    public void CharLiteralNodePresent()
    {
        ProgramHir hir = HirAssert.Hir("fn main(){ var c = 'A'; }");
        LetHir let = hir.Functions[0].Body
            .Statements.OfType<LetHir>()
            .First();
        var charNode = let.Init as CharHir;
        Assert.NotNull(charNode);
        Assert.Equal('A', charNode!.Value);
    }

    [Fact]
    public void EmptyProgram()
    {
        ProgramHir hir = HirAssert.Hir("fn main() {}");
        Assert.Single(hir.Functions);
        Assert.Equal("main", hir.Functions[0].Name);
    }

    [Fact]
    public void FactorialHir_OK()
    {
        var src = @"
            fn fact(n) {
                if (n <= 1) return 1;
                return n * fact(n - 1);
            }";
        ProgramHir hir = HirAssert.Hir(src);
        FuncHir fact = hir.Functions.Single(f => f.Name == "fact");

        // body is a Block with two statements
        Assert.Equal(2, fact.Body.Statements.Count);
    }

    [Fact]
    public void ForLoop_ExpressionLists_AreDesugaredToWhile()
    {
        var src = @"
            fn main() {
                var i = 0;
                for (i = 0, j = 10; i < j; i = i + 1, j = j - 1) {
                    continue;
                }
            }";
        ProgramHir hir = HirAssert.Hir(src);
        var body = hir.Functions[0].Body;

        // for -> { init; while (cond) { body; iter... } }
        WhileHir whileStmt = body.Statements.OfType<BlockHir>().Single().Statements.OfType<WhileHir>().Single();
        var whileBody = (BlockHir)whileStmt.Body;

        // Внутри while-body должен быть блок с итераторами: два ExprStmt(assign)
        // По нашему билдеру это второй элемент: { <original-body>, <iter-block> }
        Assert.True(whileBody.Statements.Count >= 2);
        var iterBlock = Assert.IsType<BlockHir>(whileBody.Statements[1]);
        List<ExprStmtHir> iterExprStmts = iterBlock.Statements.OfType<ExprStmtHir>().ToList();
        Assert.Equal(2, iterExprStmts.Count);
        Assert.All(iterExprStmts, es => Assert.IsType<BinHir>(es.Expr));
        Assert.All(iterExprStmts, es => Assert.Equal(BinOp.Assign, ((BinHir)es.Expr!).Op));
    }

    [Fact]
    public void IndexAndCall()
    {
        var src = @"
        fn get(a, i) { return a[i]; }
        fn main() {
            var arr = array(3);
            arr[0] = 42;
            var v = get(arr, 0);
        }";

        ProgramHir hir = HirAssert.Hir(src);

        List<ExprHir> exprs = hir.Functions
            .SelectMany(f => FlattenStmts(f.Body))
            .ToList();

        Assert.Contains(exprs, e => e is IndexHir); // arr[0]
        Assert.Contains(exprs, e => e is CallHir); // get(arr, 0)
    }

    private static IEnumerable<ExprHir> FlattenExpr(ExprHir? e)
    {
        if (e is null) yield break;

        yield return e;

        switch (e)
        {
            case BinHir b:
                foreach (ExprHir sub in FlattenExpr(b.Left)) yield return sub;
                foreach (ExprHir sub in FlattenExpr(b.Right)) yield return sub;
                break;

            case UnHir u:
                foreach (ExprHir sub in FlattenExpr(u.Operand)) yield return sub;
                break;

            case CallHir c:
                foreach (ExprHir sub in FlattenExpr(c.Callee)) yield return sub;
                foreach (ExprHir a in c.Args)
                foreach (ExprHir sub in FlattenExpr(a))
                    yield return sub;
                break;

            case IndexHir ix:
                foreach (ExprHir sub in FlattenExpr(ix.Target)) yield return sub;
                foreach (ExprHir sub in FlattenExpr(ix.Index)) yield return sub;
                break;
        }
    }

    private static IEnumerable<ExprHir> FlattenStmts(StmtHir s) => s switch
    {
        BlockHir b => b.Statements.SelectMany(FlattenStmts),

        LetHir v => v.Init is null
            ? []
            : FlattenExpr(v.Init),

        ExprStmtHir e => e.Expr is null
            ? []
            : FlattenExpr(e.Expr),

        IfHir i => FlattenExpr(i.Cond)
            .Concat(FlattenStmts(i.Then))
            .Concat(i.Else is not null ? FlattenStmts(i.Else) : []),

        WhileHir w => FlattenExpr(w.Cond)
            .Concat(FlattenStmts(w.Body)),

        _ => []
    };
}
