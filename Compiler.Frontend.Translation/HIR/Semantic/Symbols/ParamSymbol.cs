using Compiler.Frontend.Translation.HIR.Semantic.Symbols.Abstractions;

namespace Compiler.Frontend.Translation.HIR.Semantic.Symbols;

/// <summary>
///     Parameter symbol (scoped within a function).
/// </summary>
internal sealed record ParamSymbol(
    string Name) : Symbol;
