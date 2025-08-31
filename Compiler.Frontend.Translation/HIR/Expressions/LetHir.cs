using Compiler.Frontend.Translation.HIR.Expressions.Abstractions;
using Compiler.Frontend.Translation.HIR.Statements.Abstractions;
using Compiler.Frontend.Translation.HIR.Stringify;

namespace Compiler.Frontend.Translation.HIR.Expressions;

public sealed record LetHir(
    string Name,
    ExprHir? Init,
    SourceSpan Span) : StmtHir(Span)
{
    public override string ToString()
    {
        return Init is null
            ? $"let {Name}"
            : $"let {Name} = {Init}";
    }
}
