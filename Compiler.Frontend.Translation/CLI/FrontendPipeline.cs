using Antlr4.Runtime;

using Compiler.Frontend.Translation.HIR;
using Compiler.Frontend.Translation.HIR.Common;
using Compiler.Frontend.Translation.HIR.Semantic;
using Compiler.Frontend.Translation.MIR;
using Compiler.Frontend.Translation.MIR.Common;

namespace Compiler.Frontend.Translation.CLI;

public static class FrontendPipeline
{
    public static ProgramHir BuildHir(
        string src,
        bool verbose = false)
    {
        var str = new AntlrInputStream(src);
        var lexer = new global::MiniLangLexer(str);
        var tokens = new CommonTokenStream(lexer);
        var parser = new MiniLangParser(tokens);

        var listenerLexer = new ErrorListener<int>();
        var listenerParser = new ErrorListener<IToken>();
        lexer.AddErrorListener(listenerLexer);
        parser.AddErrorListener(listenerParser);

        MiniLangParser.ProgramContext? tree = parser.program();
        ProgramHir hir = new HirBuilder().Build(tree);

        if (verbose)
        {
            if (listenerLexer.HadError || listenerParser.HadError)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("error in parse");
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("parse completed");
            }

            Console.ResetColor();
        }

        new SemanticChecker().Check(hir);

        return hir;
    }

    public static MirModule BuildMir(
        ProgramHir hir)
    {
        return new HirToMir().Lower(hir);
    }
}
