using System;
using System.Collections.Generic;
using System.Linq;

using Antlr4.Runtime;
using Antlr4.Runtime.Tree;

using Compiler.Frontend;
using Compiler.Frontend.Translation.HIR.Common;
using Compiler.Frontend.Translation.HIR.Expressions;
using Compiler.Frontend.Translation.HIR.Expressions.Abstractions;
using Compiler.Frontend.Translation.HIR.Statements;
using Compiler.Frontend.Translation.HIR.Statements.Abstractions;
using Compiler.Frontend.Translation.HIR.Stringify;

namespace Compiler.Frontend.Translation.HIR;

public sealed class HirBuilder : MiniLangParserBaseVisitor<object>
{
    private static SourceSpan Span(ParserRuleContext ctx) =>
        new(ctx.Start.Line, ctx.Start.Column, ctx.Stop.Line, ctx.Stop.Column);

    private readonly static Dictionary<string, BinOp> BinMap = new()
    {
        ["="]  = BinOp.Assign,
        ["+"]  = BinOp.Add,   ["-"] = BinOp.Sub,
        ["*"]  = BinOp.Mul,   ["/"] = BinOp.Div,   ["%"] = BinOp.Mod,
        ["<"]  = BinOp.Lt,    ["<="]= BinOp.Le,    [">"] = BinOp.Gt, [">="]= BinOp.Ge,
        ["=="] = BinOp.Eq,    ["!="]= BinOp.Ne,
        ["&&"] = BinOp.And,   ["||"]= BinOp.Or
    };

    private readonly static Dictionary<string, UnOp> UnMap = new()
    {
        ["-"] = UnOp.Neg, ["!"] = UnOp.Not, ["+"] = UnOp.Plus
    };

    public ProgramHir Build(MiniLangParser.ProgramContext ctx) => (ProgramHir)VisitProgram(ctx);

    public override object VisitProgram(MiniLangParser.ProgramContext ctx)
    {
        var funcs = ctx.functionDecl().Select(f => (FuncHir)Visit(f)).ToList();
        return new ProgramHir(funcs);
    }

    public override object VisitFunctionDecl(MiniLangParser.FunctionDeclContext ctx)
    {
        var name = ctx.ID().GetText();
        var @params = ctx.paramList() is null
            ? []
            : ctx.paramList().ID().Select(t => t.GetText()).ToList();
        var body = (BlockHir)Visit(ctx.block());
        return new FuncHir(name, @params, body, Span(ctx));
    }

    public override object VisitBlock(MiniLangParser.BlockContext ctx)
    {
        var stmts = ctx.statement().Select(s => (StmtHir)Visit(s)).ToList();
        return new BlockHir(stmts, Span(ctx));
    }

    public override object VisitStatement(MiniLangParser.StatementContext ctx)
    {
        if (ctx.variableDecl() != null) return Visit(ctx.variableDecl());
        if (ctx.ifStmt() != null) return Visit(ctx.ifStmt());
        if (ctx.whileStmt() != null) return Visit(ctx.whileStmt());
        if (ctx.forStmt() != null) return Visit(ctx.forStmt());
        if (ctx.breakStmt() != null) return new BreakHir(Span(ctx));
        if (ctx.continueStmt() != null) return new ContinueHir(Span(ctx));
        if (ctx.returnStmt() != null) return Visit(ctx.returnStmt());
        if (ctx.block() != null) return Visit(ctx.block());
        if (ctx.exprStmt() != null) return Visit(ctx.exprStmt());
        throw new ArgumentException("Unknown statement variant");
    }

    public override object VisitVariableDecl(MiniLangParser.VariableDeclContext ctx)
    {
        var id = ctx.ID().GetText();
        var init = ctx.expression() is null ? null : (ExprHir)Visit(ctx.expression());
        return new LetHir(id, init, Span(ctx));
    }

    public override object VisitIfStmt(MiniLangParser.IfStmtContext ctx) =>
        new IfHir(
            (ExprHir)Visit(ctx.expression()),
            (StmtHir)Visit(ctx.statement(0)),
            ctx.ELSE() != null ? (StmtHir)Visit(ctx.statement(1)) : null,
            Span(ctx));

    public override object VisitWhileStmt(MiniLangParser.WhileStmtContext ctx) =>
        new WhileHir(
            (ExprHir)Visit(ctx.expression()),
            (StmtHir)Visit(ctx.statement()),
            Span(ctx));

    // for(init; cond; iter) stmt  =>  { init; while(cond ?? true) { stmt; iter...; } }
    public override object VisitForStmt(MiniLangParser.ForStmtContext ctx)
    {
        var span = Span(ctx);

        StmtHir? init = null;
        if (ctx.variableDecl() is not null)
            init = (StmtHir)Visit(ctx.variableDecl());
        else if (ctx.expressionList(0) is not null)
            init = ExprListToBlock(ctx.expressionList(0));

        var cond = ctx.expression() is null
            ? new BoolHir(true, span)
            : (ExprHir)Visit(ctx.expression());

        var body = (StmtHir)Visit(ctx.statement());

        var iterBlock = ctx.expressionList(1) is null
            ? new BlockHir([], span)
            : ExprListToBlock(ctx.expressionList(1));

        var whileBody = new BlockHir([body, iterBlock], span);

        var list = new List<StmtHir>();
        if (init is not null) list.Add(init);
        list.Add(new WhileHir(cond, whileBody, span));
        return new BlockHir(list, span);
    }

    private BlockHir ExprListToBlock(MiniLangParser.ExpressionListContext listCtx)
    {
        var stmts = listCtx.expression()
            .Select(e => (StmtHir)new ExprStmtHir((ExprHir)Visit(e), Span(e)))
            .ToList();
        return new BlockHir(stmts, Span(listCtx));
    }

    public override object VisitReturnStmt(MiniLangParser.ReturnStmtContext ctx) =>
        new ReturnHir(ctx.expression() is null ? null : (ExprHir)Visit(ctx.expression()), Span(ctx));

    public override object VisitExprStmt(MiniLangParser.ExprStmtContext ctx) =>
        new ExprStmtHir(ctx.expression() is null ? null : (ExprHir)Visit(ctx.expression()), Span(ctx));

    public override object VisitIdentifier(MiniLangParser.IdentifierContext ctx) =>
        new VarHir(ctx.ID().GetText(), Span(ctx));

    public override object VisitIntLiteral(MiniLangParser.IntLiteralContext ctx) =>
        new IntHir(long.Parse(ctx.INT().GetText()), Span(ctx));

    public override object VisitBoolTrue(MiniLangParser.BoolTrueContext ctx) =>
        new BoolHir(true, Span(ctx));

    public override object VisitBoolFalse(MiniLangParser.BoolFalseContext ctx) =>
        new BoolHir(false, Span(ctx));

    public override object VisitCharLiteral(MiniLangParser.CharLiteralContext ctx) =>
        new CharHir(UnescapeChar(ctx.CHAR().GetText()), Span(ctx));

    public override object VisitStringLiteral(MiniLangParser.StringLiteralContext ctx) =>
        new StringHir(UnescapeString(ctx.STRING().GetText()), Span(ctx));

    public override object VisitParens(MiniLangParser.ParensContext ctx) => Visit(ctx.expression());

    public override object VisitUnary(MiniLangParser.UnaryContext ctx) =>
        ctx.unary() is null
            ? Visit(ctx.postfixExpr())
            : new UnHir(UnMap[ctx.children[0].GetText()], (ExprHir)Visit(ctx.unary()), Span(ctx));

    private ExprHir BuildLeftAssoc<T>(IReadOnlyList<T> terms, IReadOnlyList<ITerminalNode> ops)
        where T : ParserRuleContext
    {
        var e = (ExprHir)Visit(terms[0]);
        for (int i = 0; i < ops.Count; i++)
        {
            var op = BinMap[ops[i].GetText()];
            e = new BinHir(op, e, (ExprHir)Visit(terms[i + 1]), Span(terms[i]));
        }
        return e;
    }

    private static List<ITerminalNode> ExtractOps(ParserRuleContext ctx, params string[] targets)
    {
        var list = new List<ITerminalNode>();
        foreach (var c in ctx.children)
            if (c is ITerminalNode t && Array.Exists(targets, s => s == t.Symbol.Text))
                list.Add(t);
        return list;
    }

    public override object VisitMultiplication(MiniLangParser.MultiplicationContext ctx) =>
        ctx.children.Count == 1
            ? Visit(ctx.unary(0))
            : BuildLeftAssoc(ctx.unary(), ExtractOps(ctx, "*", "/", "%"));

    public override object VisitAddition(MiniLangParser.AdditionContext ctx) =>
        ctx.children.Count == 1
            ? Visit(ctx.multiplication(0))
            : BuildLeftAssoc(ctx.multiplication(), ExtractOps(ctx, "+", "-"));

    public override object VisitComparison(MiniLangParser.ComparisonContext ctx) =>
        ctx.children.Count == 1
            ? Visit(ctx.addition(0))
            : BuildLeftAssoc(ctx.addition(), ExtractOps(ctx, "<", "<=", ">", ">="));

    public override object VisitEquality(MiniLangParser.EqualityContext ctx) =>
        ctx.children.Count == 1
            ? Visit(ctx.comparison(0))
            : BuildLeftAssoc(ctx.comparison(), ExtractOps(ctx, "==", "!="));

    public override object VisitLogicalAnd(MiniLangParser.LogicalAndContext ctx) =>
        ctx.AND_AND().Length == 0
            ? Visit(ctx.equality(0))
            : BuildLeftAssoc(ctx.equality(), ctx.AND_AND());

    public override object VisitLogicalOr(MiniLangParser.LogicalOrContext ctx) =>
        ctx.OR_OR().Length == 0
            ? Visit(ctx.logicalAnd(0))
            : BuildLeftAssoc(ctx.logicalAnd(), ctx.OR_OR());

    public override object VisitAssign(MiniLangParser.AssignContext ctx) =>
        new BinHir(BinOp.Assign, (ExprHir)Visit(ctx.postfixExpr()), (ExprHir)Visit(ctx.assignment()), Span(ctx));

    public override object VisitPostfixExpr(MiniLangParser.PostfixExprContext ctx)
    {
        var expr = (ExprHir)Visit(ctx.primary());
        foreach (var child in ctx.children)
        {
            switch (child)
            {
                case MiniLangParser.IndexSuffixContext idx:
                    expr = new IndexHir(expr, (ExprHir)Visit(idx.expression()), Span(idx));
                    break;

                case MiniLangParser.CallSuffixContext call:
                    var args = call.argumentList() is null
                        ? []
                        : call.argumentList().expression().Select(e => (ExprHir)Visit(e)).ToList();
                    expr = new CallHir(expr, args, Span(call));
                    break;
            }
        }
        return expr;
    }

    private static char UnescapeChar(string s)
    {
        if (s[1] != '\\') return s[1];
        return s[2] switch { 'n' => '\n', 'r' => '\r', 't' => '\t', '\\' => '\\', '\'' => '\'', '"' => '"', _ => s[2] };
    }

    private static string UnescapeString(string s) =>
        s.Substring(1, s.Length - 2).Replace("\\\"", "\"").Replace("\\\\", "\\");
}