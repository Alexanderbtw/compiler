using Compiler.Frontend.Translation.HIR.Stringify;

namespace Compiler.Frontend.Translation.HIR.Statements.Abstractions;

/// <summary>
///     Base type for HIR statements.
/// </summary>
public abstract record StmtHir(
    SourceSpan Span);
