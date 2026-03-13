using Compiler.Frontend.Translation.HIR.Expressions.Abstractions;
using Compiler.Frontend.Translation.HIR.Statements.Abstractions;
using Compiler.Frontend.Translation.HIR.Stringify;

namespace Compiler.Frontend.Translation.HIR.Statements;

/// <summary>
///     Variable declaration with an optional initializer.
/// </summary>
public sealed record VarDeclHir(
    string Name,
    ExprHir? Init,
    SourceSpan Span) : StmtHir(Span)
{
    public override string ToString()
    {
        return Init is null
            ? $"var {Name}"
            : $"var {Name} = {Init}";
    }
}
