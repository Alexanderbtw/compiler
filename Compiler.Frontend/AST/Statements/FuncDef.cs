using System.Collections.Generic;

namespace Compiler.Frontend.AST.Statements;

public sealed record FuncDef(string Name, List<string> Params, Block Body) : Stmt
{
    public override string ToString()
    {
        var paramList = Params is { Count: > 0 }
            ? string.Join(", ", Params)
            : "";
        return $"fn {Name}({paramList}) {Body}";
    }
}
