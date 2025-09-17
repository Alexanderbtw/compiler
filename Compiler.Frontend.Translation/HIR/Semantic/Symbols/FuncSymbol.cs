using Compiler.Frontend.Translation.HIR.Semantic.Symbols.Abstractions;

namespace Compiler.Frontend.Translation.HIR.Semantic.Symbols;

/// <summary>
///     Function symbol with a name and a list of parameter names.
/// </summary>
internal sealed record FuncSymbol(
    string Name,
    IReadOnlyList<string> Parameters) : Symbol;
