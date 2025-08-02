using Compiler.Frontend.AST.Expressions;

namespace Compiler.Frontend.AST.Statements;

public sealed record IfStmt(Expr Cond, Stmt Then, Stmt? Else) : Stmt;