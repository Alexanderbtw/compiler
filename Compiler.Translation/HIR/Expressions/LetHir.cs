using Compiler.Translation.HIR.Expressions.Abstractions;
using Compiler.Translation.HIR.Statements.Abstractions;
using Compiler.Translation.HIR.Stringify;

namespace Compiler.Translation.HIR.Expressions;

public sealed record LetHir(string Name, ExprHir? Init, SourceSpan Span) : StmtHir(Span)
{
    public override string ToString() => Init is null ? $"let {Name}" : $"let {Name} = {Init}";
}