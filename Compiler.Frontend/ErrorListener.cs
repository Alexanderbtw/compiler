using Antlr4.Runtime;

namespace Compiler.Frontend;

public sealed class ErrorListener<TSymbol> : ConsoleErrorListener<TSymbol>
{
    private readonly List<string> _diagnostics = [];

    public IReadOnlyList<string> Diagnostics => _diagnostics;

    public bool HadError => _diagnostics.Count > 0;

    public override void SyntaxError(
        TextWriter output,
        IRecognizer recognizer,
        TSymbol offendingSymbol,
        int line,
        int col,
        string msg,
        RecognitionException e)
    {
        string symbolText = offendingSymbol?.ToString() ?? "<unknown>";

        _diagnostics.Add($"line {line}:{col} {msg} (offending: {symbolText})");
    }
}
