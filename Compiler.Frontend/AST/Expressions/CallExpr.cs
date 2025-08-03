using System.Collections.Generic;
using System.Linq;

namespace Compiler.Frontend.AST.Expressions;

public sealed record CallExpr(Expr Callee, List<Expr> A) : Expr
{
    public override string ToString()
    {
        string args = A is { Count: > 0 }
            ? string.Join(", ", A.Select(e => e.ToString()))
            : "";
        return $"{Callee}({args})";
    }
}
