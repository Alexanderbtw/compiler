using Antlr4.Runtime;

using Compiler.Frontend;
using Compiler.Translation.HIR;
using Compiler.Translation.HIR.Common;
using Compiler.Translation.HIR.Semantic;
using Compiler.Translation.HIR.Semantic.Exceptions;

namespace Compiler.Tests.Semantic;

public class SemanticCheckerTests
{
    [Fact]
    public void Accepts_Factorial()
    {
        CheckOk(
            @"
            fn fact(n){
                if(n<=1) return 1;
                return n*fact(n-1);
            }
            fn main(){ print(fact(5)); }
        ");
    }

    [Fact]
    public void Rejects_BreakOutsideLoop()
    {
        CheckFails(
            @"
            fn main(){ break; }
        ",
            "'break' used outside loop");
    }

    [Fact]
    public void Rejects_DuplicateParam()
    {
        CheckFails(
            @"
            fn f(a,a){ return 0; }
            fn main(){}
        ",
            "parameter 'a' already defined");
    }

    [Fact]
    public void Rejects_UndeclaredVariable()
    {
        CheckFails(
            @"
            fn main(){ x = 3; }
        ",
            "identifier 'x' not in scope");
    }

    [Fact]
    public void Rejects_WrongArity_NonBuiltin()
    {
        CheckFails(
            @"
            fn g(a,b){ return 0; }
            fn main(){ g(1); }
        ",
            "expects 2 args, got 1");
    }

    private static ProgramHir BuildHir(string src)
    {
        ICharStream? input = CharStreams.fromString(src);
        var lexer = new MiniLangLexer(input);
        var tokens = new CommonTokenStream(lexer);
        var parser = new MiniLangParser(tokens);
        ProgramHir hir = new HirBuilder().Build(parser.program());
        return hir;
    }

    private static void CheckFails(string src, string expectedMsgPart)
    {
        var checker = new SemanticChecker();
        var ex = Assert.Throws<SemanticException>(() => checker.Check(BuildHir(src)));
        Assert.Contains(expectedMsgPart, ex.Message);
    }

    private static void CheckOk(string src)
    {
        var checker = new SemanticChecker();
        checker.Check(BuildHir(src)); // must not throw
    }
}
