using System.Collections.Generic;

namespace Compiler.Frontend.AST.Statements;

public sealed record FuncDef(string Name, List<string> Params, Block Body) : Stmt;
