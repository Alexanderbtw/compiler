using System.Text;

using Antlr4.Runtime;

using Compiler.Frontend;
using Compiler.Translation.HIR;
using Compiler.Translation.HIR.Common;
using Compiler.Translation.HIR.Semantic;

namespace Compiler.Tests.Interpretation;

internal static class Utils
{
    public static (object? value, string stdout) Run(string src, bool time = false)
    {
        ProgramHir hir = BuildHir(src);
        var interp = new Interpreter.Interpreter(hir);

        var sb = new StringBuilder();
        using var writer = new StringWriter(sb);
        TextWriter old = Console.Out;
        Console.SetOut(writer);
        object? ret = interp.Run(time);
        Console.SetOut(old);

        return (ret, sb.ToString().TrimEnd());
    }
    private static ProgramHir BuildHir(string src)
    {
        MiniLangParser parser = CreateParser(src);
        MiniLangParser.ProgramContext? tree = parser.program();
        ProgramHir hir = new HirBuilder().Build(tree);
        new SemanticChecker().Check(hir);
        return hir;
    }
    private static MiniLangParser CreateParser(string src)
    {
        var str = new AntlrInputStream(src);
        var lexer = new MiniLangLexer(str);
        var tokens = new CommonTokenStream(lexer);
        var parser = new MiniLangParser(tokens);

        return parser;
    }
}
