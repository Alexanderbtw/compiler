using System.Collections.Generic;

namespace Compiler.Translation.Semantic.Symbols;

internal record FuncSymbol(
    string Name,
    IReadOnlyList<string> Parameters) : Symbol;