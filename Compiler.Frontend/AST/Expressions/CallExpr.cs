using System.Collections.Generic;

namespace Compiler.Frontend.AST.Expressions;

public sealed record CallExpr(Expr Callee, List<Expr> A) : Expr;
