namespace Compiler.Tests.Semantic;

public class SemanticCheckerTests
{
    [Fact]
    public void Accepts_Factorial()
    {
        TestUtils.AssertSemanticOk(
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
        TestUtils.AssertSemanticFails(
            src: @"fn main(){ break; }",
            expectedSubstring: "'break' used outside loop");
    }

    [Fact]
    public void Rejects_DuplicateParam()
    {
        TestUtils.AssertSemanticFails(
            src: @"
            fn f(a,a){ return 0; }
            fn main(){}
        ",
            expectedSubstring: "parameter 'a' already defined");
    }

    [Fact]
    public void Rejects_UndeclaredVariable()
    {
        TestUtils.AssertSemanticFails(
            src: @"fn main(){ x = 3; }",
            expectedSubstring: "identifier 'x' not in scope");
    }

    [Fact]
    public void Rejects_WrongArity_NonBuiltin()
    {
        TestUtils.AssertSemanticFails(
            src: @"
            fn g(a,b){ return 0; }
            fn main(){ g(1); }
        ",
            expectedSubstring: "expects 2 args, got 1");
    }
}
