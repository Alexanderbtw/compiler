using Compiler.Frontend.Translation.HIR.Expressions.Abstractions;
using Compiler.Frontend.Translation.HIR.Statements.Abstractions;
using Compiler.Frontend.Translation.HIR.Stringify;

namespace Compiler.Frontend.Translation.HIR.Expressions;

public sealed record ReturnHir(
    ExprHir? Expr,
    SourceSpan Span) : StmtHir(Span)
{
    public override string ToString()
    {
        return Expr is null
            ? "return"
            : $"return {Expr}";
    }
}
