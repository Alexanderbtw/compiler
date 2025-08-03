using Antlr4.Runtime;

using Compiler.Frontend;

namespace Compiler.Tests.Interpretation;

internal static class Utils
{
    public static MiniLangParser CreateParser(string src)
    {
        var str = new AntlrInputStream(src);
        var lexer = new MiniLangLexer(str);
        var tokens = new CommonTokenStream(lexer);
        var parser = new MiniLangParser(tokens);

        return parser;
    }
}
