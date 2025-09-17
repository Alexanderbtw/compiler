using Compiler.Frontend.Translation.HIR.Semantic.Symbols.Abstractions;

namespace Compiler.Frontend.Translation.HIR.Semantic.Symbols;

/// <summary>
///     Local variable symbol.
/// </summary>
internal sealed record VarSymbol(
    string Name) : Symbol;
