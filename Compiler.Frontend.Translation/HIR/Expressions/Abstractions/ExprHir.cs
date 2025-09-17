using Compiler.Frontend.Translation.HIR.Stringify;

namespace Compiler.Frontend.Translation.HIR.Expressions.Abstractions;

/// <summary>
///     Base type for all HIR expressions.
///     Carries a source span for diagnostics.
/// </summary>
public abstract record ExprHir(
    SourceSpan Span);
