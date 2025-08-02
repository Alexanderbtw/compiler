using System;
using System.Collections.Generic;
using System.Linq;

using Antlr4.Runtime;
using Antlr4.Runtime.Tree;

using Compiler.Frontend.AST.Expressions;
using Compiler.Frontend.AST.Statements;

namespace Compiler.Frontend.AST;

/// <summary>
///     Converts the ANTLR parse-tree into the immutable AST records defined in Ast.cs.
///     Pure visitor – no symbol-table logic here.
/// </summary>
public sealed class AstBuilder : MiniLangParserBaseVisitor<object>
{
    public ProgramAst Build(MiniLangParser.ProgramContext ctx) => (ProgramAst)Visit(ctx);

    private Expr BuildLeftAssoc<T>(IReadOnlyList<T> terms, IReadOnlyList<ITerminalNode> ops)
        where T : class, IParseTree
    {
        var expr = (Expr)Visit(terms[0]);
        for (var i = 0; i < ops.Count; ++i)
            expr = new BinExpr(ops[i].GetText(), expr, (Expr)Visit(terms[i + 1]));
        return expr;
    }

    private static List<ITerminalNode> ExtractOps(ParserRuleContext ctx, params string[] targets)
    {
        var list = new List<ITerminalNode>();
        foreach (IParseTree? c in ctx.children)
            if (c is ITerminalNode t && Array.Exists(targets, s => s == t.Symbol.Text))
                list.Add(t);
        return list;
    }

    private static List<string> GetIdList(IReadOnlyList<ITerminalNode> ids)
    {
        var list = new List<string>(ids.Count);
        foreach (ITerminalNode id in ids) list.Add(id.GetText());
        return list;
    }

    private Stmt ToExprBlock(MiniLangParser.ExpressionListContext listCtx)
    {
        var stmts = new List<Stmt>();
        foreach (MiniLangParser.ExpressionContext? e in listCtx.expression())
            stmts.Add(new ExprStmt((Expr)Visit(e))); // ← same here
        return new Block(stmts);
    }

    private static char UnescapeChar(string s) // handles simple '\x' escapes
    {
        if (s[1] != '\\') return s[1];
        return s[2] switch
        {
            'n' => '\n', 'r' => '\r', 't' => '\t', '\\' => '\\', '\'' => '\'', '"' => '"',
            _ => s[2] // unknown – leave raw
        };
    }

    private static string UnescapeString(string s) =>
        s.Substring(1, s.Length - 2).Replace("\\\"", "\"").Replace("\\\\", "\\");

    public override object VisitAddition(MiniLangParser.AdditionContext ctx) => ctx.children.Count == 1
        ? Visit(ctx.multiplication(0))
        : BuildLeftAssoc(ctx.multiplication(), ExtractOps(ctx, "+", "-"));

    public override List<Expr> VisitArgumentList(MiniLangParser.ArgumentListContext ctx)
    {
        var list = new List<Expr>();
        foreach (MiniLangParser.ExpressionContext? e in ctx.expression())
            list.Add((Expr)Visit(e)); // use this visitor
        return list;
    }

    public override object VisitAssign(MiniLangParser.AssignContext ctx) => new BinExpr(
        "=",
        (Expr)Visit(ctx.postfixExpr()),
        (Expr)Visit(ctx.assignment()));

    public override object VisitBlock(MiniLangParser.BlockContext ctx)
    {
        var stmts = new List<Stmt>();
        foreach (MiniLangParser.StatementContext? s in ctx.statement())
            stmts.Add((Stmt)Visit(s));
        return new Block(stmts);
    }
    public override object VisitBoolFalse(MiniLangParser.BoolFalseContext ctx) => new BoolLit(false);

    public override object VisitBoolTrue(MiniLangParser.BoolTrueContext ctx) => new BoolLit(true);

    public override object VisitCharLiteral(MiniLangParser.CharLiteralContext ctx) =>
        new CharLit(UnescapeChar(ctx.CHAR().GetText()));

    public override object VisitComparison(MiniLangParser.ComparisonContext ctx) => ctx.children.Count == 1
        ? Visit(ctx.addition(0))
        : BuildLeftAssoc(ctx.addition(), ExtractOps(ctx, "<", "<=", ">", ">="));

    public override object VisitEquality(MiniLangParser.EqualityContext ctx) => ctx.children.Count == 1
        ? Visit(ctx.comparison(0))
        : BuildLeftAssoc(ctx.comparison(), ExtractOps(ctx, "==", "!="));

    private List<Expr> VisitExprList(MiniLangParser.ExpressionListContext ctx)
    {
        var list = new List<Expr>();
        foreach (MiniLangParser.ExpressionContext? e in ctx.expression())
            list.Add((Expr)Visit(e));
        return list;
    }

    public override object VisitExprStmt(MiniLangParser.ExprStmtContext ctx) =>
        new ExprStmt(ctx.expression() == null ? null : (Expr)Visit(ctx.expression()));

    public override object VisitForStmt(MiniLangParser.ForStmtContext ctx)
    {
        Stmt? init = null;
        if (ctx.variableDecl() != null)
            init = (Stmt)Visit(ctx.variableDecl());
        else if (ctx.expressionList(0) != null)
            init = ToExprBlock(ctx.expressionList(0));

        Expr? cond = ctx.expression() != null ? (Expr)Visit(ctx.expression()) : null;

        List<Expr>? iter = ctx.expressionList(1) != null
            ? VisitExprList(ctx.expressionList(1))
            : null;

        return new ForStmt(init, cond, iter, (Stmt)Visit(ctx.statement()));
    }

    public override object VisitFunctionDecl(MiniLangParser.FunctionDeclContext ctx)
    {
        string? name = ctx.ID().GetText();
        List<string> paramsList = ctx.paramList() != null
            ? GetIdList(ctx.paramList().ID())
            : new List<string>();

        var body = (Block)Visit(ctx.block());
        return new FuncDef(name, paramsList, body);
    }
    public override object VisitIdentifier(MiniLangParser.IdentifierContext ctx) =>
        new VarExpr(ctx.ID().GetText());

    public override object VisitIfStmt(MiniLangParser.IfStmtContext ctx) => new IfStmt(
        (Expr)Visit(ctx.expression()),
        (Stmt)Visit(ctx.statement(0)),
        ctx.ELSE() != null ? (Stmt)Visit(ctx.statement(1)) : null);

    public override object VisitIntLiteral(MiniLangParser.IntLiteralContext ctx) =>
        new IntLit(long.Parse(ctx.INT().GetText()));

    public override object VisitLogicalAnd(MiniLangParser.LogicalAndContext ctx) =>
        ctx.AND_AND().Length == 0
            ? Visit(ctx.equality(0))
            : BuildLeftAssoc(ctx.equality(), ctx.AND_AND());

    public override object VisitLogicalOr(MiniLangParser.LogicalOrContext ctx) => ctx.OR_OR().Length == 0
        ? Visit(ctx.logicalAnd(0))
        : BuildLeftAssoc(ctx.logicalAnd(), ctx.OR_OR());

    public override object VisitMultiplication(MiniLangParser.MultiplicationContext ctx) =>
        ctx.children.Count == 1
            ? Visit(ctx.unary(0))
            : BuildLeftAssoc(ctx.unary(), ExtractOps(ctx, "*", "/", "%"));

    public override object VisitPostfixExpr(MiniLangParser.PostfixExprContext ctx)
    {
        var expr = (Expr)Visit(ctx.primary());

        // iterate over *rule* children (tokens are ignored automatically)
        foreach (IParseTree? child in ctx.children)
        {
            switch (child)
            {
                // ── a [...] suffix ───────────────────────────────
                case MiniLangParser.IndexSuffixContext idx:
                {
                    var indexExpr = (Expr)Visit(idx.expression());
                    expr = new IndexExpr(expr, indexExpr);
                    break;
                }

                // ── a (...) suffix ───────────────────────────────
                case MiniLangParser.CallSuffixContext call:
                {
                    List<Expr> args = call.argumentList() == null
                        ? new List<Expr>()
                        : call.argumentList()
                            .expression()
                            .Select(e => (Expr)Visit(e))
                            .ToList();

                    expr = new CallExpr(expr, args);
                    break;
                }

                // terminals like '[' '(' ')' ']' fall through
            }
        }

        return expr;
    }

    public override object VisitProgram(MiniLangParser.ProgramContext ctx)
    {
        List<FuncDef> funcs = ctx.functionDecl().Select(f => (FuncDef)Visit(f)).ToList();
        return new ProgramAst(funcs);
    }

    public override object VisitReturnStmt(MiniLangParser.ReturnStmtContext ctx) =>
        new Return(ctx.expression() == null ? null : (Expr)Visit(ctx.expression()));

    public override object VisitSimpleExpr(MiniLangParser.SimpleExprContext ctx) => Visit(ctx.logicalOr());

    public override object VisitStatement(MiniLangParser.StatementContext ctx)
    {
        if (ctx.variableDecl() != null) return Visit(ctx.variableDecl());
        if (ctx.ifStmt() != null) return Visit(ctx.ifStmt());
        if (ctx.whileStmt() != null) return Visit(ctx.whileStmt());
        if (ctx.forStmt() != null) return Visit(ctx.forStmt());
        if (ctx.breakStmt() != null) return new Break();
        if (ctx.continueStmt() != null) return new Continue();
        if (ctx.returnStmt() != null) return Visit(ctx.returnStmt());
        if (ctx.block() != null) return Visit(ctx.block());
        if (ctx.exprStmt() != null) return Visit(ctx.exprStmt());

        throw new ArgumentException("Unknown statement variant");
    }

    public override object VisitStringLiteral(MiniLangParser.StringLiteralContext ctx) =>
        new StringLit(UnescapeString(ctx.STRING().GetText()));

    public override object VisitUnary(MiniLangParser.UnaryContext ctx) => ctx.unary() == null
        ? Visit(ctx.postfixExpr())
        : new UnExpr(ctx.children[0].GetText(), (Expr)Visit(ctx.unary()));

    public override object VisitVariableDecl(MiniLangParser.VariableDeclContext ctx)
    {
        string? id = ctx.ID().GetText();
        Expr? init = ctx.expression() != null ? (Expr)Visit(ctx.expression()) : null;
        return new VarDecl(id, init);
    }

    public override object VisitWhileStmt(MiniLangParser.WhileStmtContext ctx) => new WhileStmt(
        (Expr)Visit(ctx.expression()),
        (Stmt)Visit(ctx.statement()));
}

public record ProgramAst(List<FuncDef> Functions); // tiny root wrapper
