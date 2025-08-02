using Compiler.Frontend.AST;
using Compiler.Frontend.AST.Expressions;
using Compiler.Frontend.AST.Statements;

using Xunit.Abstractions;

namespace Compiler.Tests.AST;

public class AstBuilderTests
{
    private readonly ITestOutputHelper _testOutputHelper;
    public AstBuilderTests(ITestOutputHelper testOutputHelper) => _testOutputHelper = testOutputHelper;
    [Fact]
    public void CharLiteralNodePresent()
    {
        ProgramAst ast = AstAssert.Ast("fn main(){ var c = 'A'; }");
        var charNode = ast.Functions[0].Body
            .Body.OfType<VarDecl>()
            .First().Init as CharLit;
        Assert.NotNull(charNode);
        Assert.Equal('A', charNode!.Value);
    }

    [Fact]
    public void EmptyProgram()
    {
        ProgramAst ast = AstAssert.Ast("fn main() {}");
        Assert.Single(ast.Functions);
        Assert.Equal("main", ast.Functions[0].Name);
    }

    [Fact]
    public void FactorialAst_OK()
    {
        var src = @"
            fn fact(n) {
                if (n <= 1) return 1;
                return n * fact(n - 1);
            }";
        ProgramAst ast = AstAssert.Ast(src);
        FuncDef fact = ast.Functions.Single(f => f.Name == "fact");

        // body is a Block with two statements
        Assert.Equal(2, fact.Body.Body.Count);
    }

    private static IEnumerable<Expr> FlattenExpr(Expr e) => e switch
    {
        BinExpr b => new[] { b }
            .Concat(FlattenExpr(b.L))
            .Concat(FlattenExpr(b.R)),

        UnExpr u => new[] { u }
            .Concat(FlattenExpr(u.R)),

        CallExpr c => new[] { c }
            .Concat(FlattenExpr(c.Callee))
            .Concat(c.A.SelectMany(FlattenExpr)),

        IndexExpr i => new[] { i }
            .Concat(FlattenExpr(i.Arr))
            .Concat(FlattenExpr(i.Index)),

        _ => [e]
    };

    private static IEnumerable<Expr> FlattenStmts(Stmt s) => s switch
    {
        Block b => b.Body.SelectMany(FlattenStmts),

        VarDecl v => v.Init == null
            ? []
            : FlattenExpr(v.Init),

        ExprStmt e => e.E == null
            ? []
            : FlattenExpr(e.E),

        IfStmt i => FlattenExpr(i.Cond)
            .Concat(FlattenStmts(i.Then))
            .Concat(i.Else != null ? FlattenStmts(i.Else) : []),

        WhileStmt w => FlattenExpr(w.Cond)
            .Concat(FlattenStmts(w.Body)),

        ForStmt f => (f.Init != null ? FlattenStmts(f.Init) : [])
            .Concat(f.Iter != null ? f.Iter.SelectMany(FlattenExpr) : [])
            .Concat(f.Cond != null ? FlattenExpr(f.Cond) : [])
            .Concat(FlattenStmts(f.Body)),

        _ => []
    };

    [Fact]
    public void ForLoop_ExpressionLists()
    {
        var src = @"
            fn main() {
                var i = 0;
                for (i = 0, j = 10; i < j; i = i + 1, j = j - 1) {
                    continue;
                }
            }";
        ProgramAst ast = AstAssert.Ast(src);
        ForStmt loop = ast.Functions[0].Body
            .Body.OfType<ForStmt>()
            .Single();
        Assert.NotNull(loop.Init);
        Assert.NotNull(loop.Iter);
        Assert.Equal(2, loop.Iter!.Count);
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

        ProgramAst ast = AstAssert.Ast(src);

        // Gather every expression in the program
        List<Expr> exprs = ast.Functions
            .SelectMany(f => FlattenStmts(f.Body)) // recurse through the function body
            .ToList();

        Assert.Contains(exprs, e => e is IndexExpr); // arr[0]
        Assert.Contains(exprs, e => e is CallExpr); // get(arr, 0)
    }
}
