using System.Collections.Generic;
using System.Linq;

namespace Compiler.Frontend.AST.Statements;

public sealed record Block(List<Stmt> Body) : Stmt
{
    public override string ToString()
    {
        string stmts = string.Join(" ", Body.Select(s => s.ToString()));
        return $"{{ {stmts} }}";
    }
}
