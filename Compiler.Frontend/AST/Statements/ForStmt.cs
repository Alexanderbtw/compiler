using System.Collections.Generic;

using Compiler.Frontend.AST.Expressions;

namespace Compiler.Frontend.AST.Statements;

public sealed record ForStmt(Stmt? Init, Expr? Cond, List<Expr>? Iter, Stmt Body) : Stmt;