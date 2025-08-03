using Compiler.Frontend.AST.Expressions;

namespace Compiler.Frontend.AST.Statements;

public sealed record IfStmt(Expr Cond, Stmt Then, Stmt? Else) : Stmt
{
    public override string ToString()
    {
        var result = $"if ({Cond}) {Then}";
        if (Else is not null)
            result += $" else {Else}";
        return result;
    }
}
