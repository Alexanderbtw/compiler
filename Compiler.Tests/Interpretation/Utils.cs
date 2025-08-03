using System.Text;

using Antlr4.Runtime;

using Compiler.Frontend;
using Compiler.Frontend.AST;
using Compiler.Frontend.Interpretation;
using Compiler.Frontend.Semantic;

namespace Compiler.Tests.Interpretation;

internal static class Utils
{
    private static ProgramAst BuildAst(string src)
    {
        MiniLangParser parser = CreateParser(src);
        MiniLangParser.ProgramContext? tree = parser.program();
        ProgramAst ast = new AstBuilder().Build(tree);
        new SemanticChecker().Check(ast);
        return ast;
    }
    private static MiniLangParser CreateParser(string src)
    {
        var str = new AntlrInputStream(src);
        var lexer = new MiniLangLexer(str);
        var tokens = new CommonTokenStream(lexer);
        var parser = new MiniLangParser(tokens);

        return parser;
    }

    public static (object? value, string stdout) Run(string src, bool time = false)
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
}
