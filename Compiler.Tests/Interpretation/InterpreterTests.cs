using System.Text;

using Compiler.Frontend;
using Compiler.Frontend.AST;
using Compiler.Frontend.Interpretation;
using Compiler.Frontend.Semantic;

namespace Compiler.Tests.Interpretation;

public class InterpreterTests
{
    [Fact]
    public void BreakContinue_WorkInsideLoops()
    {
        var src = @"
            fn main(){
                var s=0;
                var i=0;
                while(i<6){
                    i=i+1;
                    if(i==3) continue;
                    if(i==5) break;
                    s=s+i;
                }
                return s;   // 1+2+4 = 7
            }";

        (object? res, _) = Run(src);
        Assert.Equal(7L, res);
    }

    private static ProgramAst BuildAst(string src)
    {
        MiniLangParser parser = Utils.CreateParser(src);
        var tree = parser.program();
        ProgramAst ast = new AstBuilder().Build(tree);
        new SemanticChecker().Check(ast);
        return ast;
    }

    [Fact]
    public void Builtins_PrintArrayClockMs()
    {
        var src = @"
            fn main(){
                var a = array(3);
                a[0] = 7;
                print(""val="", a[0]);
            }";

        (_, string stdout) = Run(src);
        Assert.Equal("val=7", stdout); // printed correctly
    }

    [Fact]
    public void Factorial_20_ReturnsExpected()
    {
        var src = @"
            fn fact(n) {
                if (n <= 1) return 1;
                return n * fact(n - 1);
            }
            fn main() { return fact(20); }";

        (object? value, _) = Run(src);
        Assert.Equal(2432902008176640000L, value);
    }

    [Fact]
    public void QuickSort_SortsTenRandoms()
    {
        var src = @"
            fn qsort(arr, lo, hi) {
                if (lo >= hi) return;
                var p = arr[(lo+hi)/2];
                var i = lo;
                var j = hi;
                while (i <= j) {
                    while (arr[i] < p)  i = i + 1;
                    while (arr[j] > p)  j = j - 1;
                    if (i <= j) {
                        var t = arr[i]; arr[i] = arr[j]; arr[j] = t;
                        i = i + 1;  j = j - 1;
                    }
                }
                qsort(arr, lo, j);
                qsort(arr, i,  hi);
            }
            fn main() {
                var a = array(10);
                var k=0;
                while (k<10){ a[k]=9-k; k=k+1; }   // 9..0
                qsort(a,0,9);
                k=0; while(k<10){ print(a[k]); k=k+1; }
            }";

        (_, string stdout) = Run(src);
        Assert.Equal("0\r\n1\r\n2\r\n3\r\n4\r\n5\r\n6\r\n7\r\n8\r\n9", stdout.Replace(" ", ""));
    }

    private static (object? value, string stdout) Run(string src, bool time = false)
    {
        ProgramAst ast = BuildAst(src);
        var interp = new Interpreter(ast);

        var sb = new StringBuilder();
        using var writer = new StringWriter(sb);
        TextWriter old = Console.Out;
        Console.SetOut(writer);
        object? ret = interp.Run(time);
        Console.SetOut(old);

        return (ret, sb.ToString().TrimEnd());
    }

    [Fact]
    public void Sieve_PrimeCountMatchesReference()
    {
        var src = @"
            fn main(){
                var N = 30000;
                var prime = array(N+1);
                var i = 2;
                while(i<=N){ prime[i]=true; i=i+1; }
                i=2;
                while(i*i<=N){
                    if(prime[i]){
                        var j=i*i;
                        while(j<=N){ prime[j]=false; j=j+i; }
                    }
                    i=i+1;
                }
                var cnt=0; i=2;
                while(i<=N){ if(prime[i]) cnt=cnt+1; i=i+1; }
                return cnt;
            }";

        (object? res, _) = Run(src);
        Assert.Equal(3245L, res); // reference count of primes ≤ 30000
    }

    [Fact]
    public void IndexExprReturnsValue()
    {
        var src = @"fn main(){ var a=array(1); a[0]=99; print(a[0]); }";
        var (_, outTxt) = Run(src);
        Assert.Equal("99", outTxt);
    }
}
