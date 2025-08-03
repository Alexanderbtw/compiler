using Compiler.Frontend.AST.Expressions;

namespace Compiler.Frontend.AST.Statements;

public sealed record VarDecl(string Name, Expr? Init) : Stmt
{
    public override string ToString() =>
        Init is null
            ? $"var {Name};"
            : $"var {Name} = {Init};";
}
