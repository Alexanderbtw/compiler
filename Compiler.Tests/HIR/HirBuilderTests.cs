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
        var hir = TestUtils.BuildHir("fn main(){ var c = 'A'; }");
        var let = hir.Functions[0].Body.Statements.OfType<LetHir>().First();
        var charNode = let.Init as CharHir;
        Assert.NotNull(charNode);
        Assert.Equal('A', charNode!.Value);
    }

    [Fact]
    public void EmptyProgram()
    {
        var hir = TestUtils.BuildHir("fn main() {}");
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
        var hir = TestUtils.BuildHir(src);
        var fact = hir.Functions.Single(f => f.Name == "fact");
        Assert.Equal(2, fact.Body.Statements.Count);
    }

    [Fact]
    public void ForLoop_ExpressionLists_AreDesugaredToWhile()
    {
        var src = @"
            fn main() {
                var i = 0;
                var j = 10;
                for (i = 0, j = 10; i < j; i = i + 1, j = j - 1) {
                    continue;
                }
            }";
        var hir = TestUtils.BuildHir(src);
        var body = hir.Functions[0].Body;

        var whileStmt = body.Statements.OfType<BlockHir>().Single().Statements.OfType<WhileHir>().Single();
        var whileBody = (BlockHir)whileStmt.Body;

        Assert.True(whileBody.Statements.Count >= 2);
        var iterBlock = Assert.IsType<BlockHir>(whileBody.Statements[1]);
        var iterExprStmts = iterBlock.Statements.OfType<ExprStmtHir>().ToList();
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
        var hir = TestUtils.BuildHir(src);

        var exprs = hir.Functions
            .SelectMany(f => TestUtils.FlattenStmts(f.Body))
            .ToList();

        Assert.Contains(exprs, e => e is IndexHir);
        Assert.Contains(exprs, e => e is CallHir);
    }
}
