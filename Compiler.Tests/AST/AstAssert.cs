using Antlr4.Runtime;

using Compiler.Frontend;
using Compiler.Frontend.AST;

namespace Compiler.Tests.AST;

internal static class AstAssert
{
    internal static ProgramAst Ast(string src)
    {
        var input = new AntlrInputStream(src);
        var lexer = new MiniLangLexer(input);
        var parser = new MiniLangParser(new CommonTokenStream(lexer));

        var listenerLexer = new ErrorListener<int>();
        var listenerParser = new ErrorListener<IToken>();

        lexer.AddErrorListener(listenerLexer);
        parser.AddErrorListener(listenerParser);

        MiniLangParser.ProgramContext? tree = parser.program();
        Assert.Equal(0, parser.NumberOfSyntaxErrors);
        return new AstBuilder().Build(tree);
    }
}
