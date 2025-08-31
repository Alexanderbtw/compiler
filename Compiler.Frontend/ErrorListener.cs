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
        base.SyntaxError(
            output: output,
            recognizer: recognizer,
            offendingSymbol: offendingSymbol,
            line: line,
            charPositionInLine: col,
            msg: msg,
            e: e);
    }
}
