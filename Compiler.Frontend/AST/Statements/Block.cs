using System.Collections.Generic;

namespace Compiler.Frontend.AST.Statements;

public sealed record Block(List<Stmt> Body) : Stmt;