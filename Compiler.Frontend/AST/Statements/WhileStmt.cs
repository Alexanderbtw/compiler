using Compiler.Frontend.AST.Expressions;

namespace Compiler.Frontend.AST.Statements;

public sealed record WhileStmt(Expr Cond, Stmt Body) : Stmt;
