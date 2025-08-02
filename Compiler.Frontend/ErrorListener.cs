using System.IO;

using Antlr4.Runtime;

namespace Compiler.Frontend;

public class ErrorListener<TSymbol> : ConsoleErrorListener<TSymbol>
{
    public bool HadError;

    public override void SyntaxError(
        TextWriter output,
        IRecognizer recognizer,
        TSymbol offendingSymbol,
        int line,
        int col,
        string msg,
        RecognitionException e)
    {
        HadError = true;
        base.SyntaxError(output, recognizer, offendingSymbol, line, col, msg, e);
    }
}
