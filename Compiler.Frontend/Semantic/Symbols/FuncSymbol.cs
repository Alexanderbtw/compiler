using System.Collections.Generic;

namespace Compiler.Frontend.Semantic.Symbols;

internal record FuncSymbol(
    string Name,
    IReadOnlyList<string> Parameters) : Symbol;