using Antlr4.Runtime;

using Compiler.Frontend;
using Compiler.Translation.HIR;
using Compiler.Translation.HIR.Common;

namespace Compiler.Tests.HIR;

internal static class AstAssert
{
    internal static ProgramHir Ast(string src)
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
        return new HirBuilder().Build(tree);
    }
}
