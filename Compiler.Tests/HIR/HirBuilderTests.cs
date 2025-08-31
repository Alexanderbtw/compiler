using Compiler.Frontend.Translation.HIR.Common;
using Compiler.Frontend.Translation.HIR.Expressions;
using Compiler.Frontend.Translation.HIR.Expressions.Abstractions;
using Compiler.Frontend.Translation.HIR.Statements;

namespace Compiler.Tests.HIR;

public class HirBuilderTests
{
    [Fact]
    public void CharLiteralNodePresent()
    {
        ProgramHir hir = TestUtils.BuildHir("fn main(){ var c = 'A'; }");
        LetHir let = hir
            .Functions[0]
            .Body
            .Statements
            .OfType<LetHir>()
            .First();

        var charNode = let.Init as CharHir;
        Assert.NotNull(charNode);
        Assert.Equal(
            expected: 'A',
            actual: charNode!.Value);
    }

    [Fact]
    public void EmptyProgram()
    {
        ProgramHir hir = TestUtils.BuildHir("fn main() {}");
        Assert.Single(hir.Functions);
        Assert.Equal(
            expected: "main",
            actual: hir.Functions[0].Name);
    }

    [Fact]
    public void FactorialHir_OK()
    {
        var src = @"
            fn fact(n) {
                if (n <= 1) return 1;
                return n * fact(n - 1);
            }";

        ProgramHir hir = TestUtils.BuildHir(src);
        FuncHir fact = hir.Functions.Single(f => f.Name == "fact");
        Assert.Equal(
            expected: 2,
            actual: fact.Body.Statements.Count);
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

        ProgramHir hir = TestUtils.BuildHir(src);
        BlockHir body = hir.Functions[0].Body;

        WhileHir whileStmt = body
            .Statements
            .OfType<BlockHir>()
            .Single()
            .Statements
            .OfType<WhileHir>()
            .Single();

        var whileBody = (BlockHir)whileStmt.Body;

        Assert.True(whileBody.Statements.Count >= 2);
        var iterBlock = Assert.IsType<BlockHir>(whileBody.Statements[1]);
        List<ExprStmtHir> iterExprStmts = iterBlock
            .Statements
            .OfType<ExprStmtHir>()
            .ToList();

        Assert.Equal(
            expected: 2,
            actual: iterExprStmts.Count);

        Assert.All(
            collection: iterExprStmts,
            action: es => Assert.IsType<BinHir>(es.Expr));

        Assert.All(
            collection: iterExprStmts,
            action: es => Assert.Equal(
                expected: BinOp.Assign,
                actual: ((BinHir)es.Expr!).Op));
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

        ProgramHir hir = TestUtils.BuildHir(src);

        List<ExprHir> exprs = hir
            .Functions
            .SelectMany(f => TestUtils.FlattenStmts(f.Body))
            .ToList();

        Assert.Contains(
            collection: exprs,
            filter: e => e is IndexHir);

        Assert.Contains(
            collection: exprs,
            filter: e => e is CallHir);
    }
}
