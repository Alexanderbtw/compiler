using Antlr4.Runtime;

using Compiler.Frontend;
using Compiler.Frontend.Translation.HIR;
using Compiler.Frontend.Translation.HIR.Common;
using Compiler.Frontend.Translation.HIR.Semantic;
using Compiler.Frontend.Translation.HIR.Semantic.Exceptions;

namespace Compiler.Tests.Semantic;

public class SemanticCheckerTests
{
    [Fact]
    public void Accepts_Factorial()
    {
        TestUtils.AssertSemanticOk(@"
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
        TestUtils.AssertSemanticFails(@"fn main(){ break; }", "'break' used outside loop");
    }

    [Fact]
    public void Rejects_DuplicateParam()
    {
        TestUtils.AssertSemanticFails(@"
            fn f(a,a){ return 0; }
            fn main(){}
        ", "parameter 'a' already defined");
    }

    [Fact]
    public void Rejects_UndeclaredVariable()
    {
        TestUtils.AssertSemanticFails(@"fn main(){ x = 3; }", "identifier 'x' not in scope");
    }

    [Fact]
    public void Rejects_WrongArity_NonBuiltin()
    {
        TestUtils.AssertSemanticFails(@"
            fn g(a,b){ return 0; }
            fn main(){ g(1); }
        ", "expects 2 args, got 1");
    }
}
