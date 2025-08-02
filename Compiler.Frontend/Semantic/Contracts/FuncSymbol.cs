using System.Collections.Generic;

namespace Compiler.Frontend.Semantic;

internal record FuncSymbol(
    string Name,
    IReadOnlyList<string> Parameters) : Symbol;