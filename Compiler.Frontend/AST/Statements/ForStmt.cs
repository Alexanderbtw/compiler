using System.Collections.Generic;
using System.Linq;

using Compiler.Frontend.AST.Expressions;

namespace Compiler.Frontend.AST.Statements;

public sealed record ForStmt(Stmt? Init, Expr? Cond, List<Expr>? Iter, Stmt Body) : Stmt
{
    public override string ToString()
    {
        string initStr = Init?.ToString().TrimEnd(';') ?? "";
        string condStr = Cond?.ToString() ?? "";
        string iterStr = Iter is null
            ? ""
            : string.Join(", ", Iter.Select(e => e.ToString()));

        return $"for ({initStr}; {condStr}; {iterStr}) {Body}";
    }
}
