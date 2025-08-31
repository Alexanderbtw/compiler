namespace Compiler.Frontend.Translation.HIR.Semantic.Symbols;

internal record FuncSymbol(
    string Name,
    IReadOnlyList<string> Parameters) : Symbol;