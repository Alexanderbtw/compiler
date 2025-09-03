using Antlr4.Runtime;
using Antlr4.Runtime.Tree;

using Compiler.Frontend.Translation.HIR.Common;
using Compiler.Frontend.Translation.HIR.Expressions;
using Compiler.Frontend.Translation.HIR.Expressions.Abstractions;
using Compiler.Frontend.Translation.HIR.Statements;
using Compiler.Frontend.Translation.HIR.Statements.Abstractions;
using Compiler.Frontend.Translation.HIR.Stringify;

namespace Compiler.Frontend.Translation.HIR;

public sealed class HirBuilder : MiniLangParserBaseVisitor<object>
{
    private static readonly Dictionary<string, BinOp> BinMap = new Dictionary<string, BinOp>
    {
        ["="] = BinOp.Assign,
        ["+"] = BinOp.Add, ["-"] = BinOp.Sub,
        ["*"] = BinOp.Mul, ["/"] = BinOp.Div, ["%"] = BinOp.Mod,
        ["<"] = BinOp.Lt, ["<="] = BinOp.Le, [">"] = BinOp.Gt, [">="] = BinOp.Ge,
        ["=="] = BinOp.Eq, ["!="] = BinOp.Ne,
        ["&&"] = BinOp.And, ["||"] = BinOp.Or
    };

    private static readonly Dictionary<string, UnOp> UnMap = new Dictionary<string, UnOp>
    {
        ["-"] = UnOp.Neg, ["!"] = UnOp.Not, ["+"] = UnOp.Plus
    };

    public ProgramHir Build(
        MiniLangParser.ProgramContext ctx)
    {
        return (ProgramHir)VisitProgram(ctx);
    }

    public override object VisitAddition(
        MiniLangParser.AdditionContext ctx)
    {
        return ctx.children.Count == 1
            ? Visit(ctx.multiplication(0))
            : BuildLeftAssoc(
                terms: ctx.multiplication(),
                ops: ExtractOps(
                    ctx: ctx,
                    "+",
                    "-"));
    }

    public override object VisitAssign(
        MiniLangParser.AssignContext ctx)
    {
        return new BinHir(
            Op: BinOp.Assign,
            Left: (ExprHir)Visit(ctx.postfixExpr()),
            Right: (ExprHir)Visit(ctx.assignment()),
            Span: Span(ctx));
    }

    public override object VisitBlock(
        MiniLangParser.BlockContext ctx)
    {
        List<StmtHir> stmts = ctx
            .statement()
            .Select(s => (StmtHir)Visit(s))
            .ToList();

        return new BlockHir(
            Statements: stmts,
            Span: Span(ctx));
    }

    public override object VisitBoolFalse(
        MiniLangParser.BoolFalseContext ctx)
    {
        return new BoolHir(
            Value: false,
            Span: Span(ctx));
    }

    public override object VisitBoolTrue(
        MiniLangParser.BoolTrueContext ctx)
    {
        return new BoolHir(
            Value: true,
            Span: Span(ctx));
    }

    public override object VisitCharLiteral(
        MiniLangParser.CharLiteralContext ctx)
    {
        return new CharHir(
            Value: UnescapeChar(
                ctx
                    .CHAR()
                    .GetText()),
            Span: Span(ctx));
    }

    public override object VisitComparison(
        MiniLangParser.ComparisonContext ctx)
    {
        return ctx.children.Count == 1
            ? Visit(ctx.addition(0))
            : BuildLeftAssoc(
                terms: ctx.addition(),
                ops: ExtractOps(
                    ctx: ctx,
                    "<",
                    "<=",
                    ">",
                    ">="));
    }

    public override object VisitEquality(
        MiniLangParser.EqualityContext ctx)
    {
        return ctx.children.Count == 1
            ? Visit(ctx.comparison(0))
            : BuildLeftAssoc(
                terms: ctx.comparison(),
                ops: ExtractOps(
                    ctx: ctx,
                    "==",
                    "!="));
    }

    public override object VisitExprStmt(
        MiniLangParser.ExprStmtContext ctx)
    {
        return new ExprStmtHir(
            Expr: ctx.expression() is null
                ? null
                : (ExprHir)Visit(ctx.expression()),
            Span: Span(ctx));
    }

    // for(init; cond; iter) stmt  =>  { init; while(cond ?? true) { stmt; iter...; } }
    public override object VisitForStmt(
        MiniLangParser.ForStmtContext ctx)
    {
        {
            SourceSpan span = Span(ctx);

            MiniLangParser.ExpressionListContext[]? exprLists = ctx.expressionList();

            StmtHir? init = null;
            MiniLangParser.ExpressionListContext? iterListCtx = null;

            if (ctx.variableDecl() is not null)
            {
                init = (StmtHir)Visit(ctx.variableDecl());

                if (exprLists.Length > 0)
                {
                    iterListCtx = exprLists[0];
                }
            }
            else
            {
                if (exprLists.Length > 0)
                {
                    init = ExprListToBlock(exprLists[0]);
                }

                if (exprLists.Length > 1)
                {
                    iterListCtx = exprLists[1];
                }
            }

            ExprHir cond = ctx.expression() is null
                ? new BoolHir(
                    Value: true,
                    Span: span)
                : (ExprHir)Visit(ctx.expression());

            var body = (StmtHir)Visit(ctx.statement());

            BlockHir iterBlock = iterListCtx is null
                ? new BlockHir(
                    Statements: [],
                    Span: span)
                : ExprListToBlock(iterListCtx);

            var whileBody = new BlockHir(
                Statements: [body, iterBlock],
                Span: span);

            var list = new List<StmtHir>();

            if (init is not null)
            {
                list.Add(init);
            }

            list.Add(
                new WhileHir(
                    Cond: cond,
                    Body: whileBody,
                    Span: span));

            return new BlockHir(
                Statements: list,
                Span: span);
        }
    }

    public override object VisitFunctionDecl(
        MiniLangParser.FunctionDeclContext ctx)
    {
        string? name = ctx
            .ID()
            .GetText();

        List<string> @params = ctx.paramList() is null
            ? []
            : ctx
                .paramList()
                .ID()
                .Select(t => t.GetText())
                .ToList();

        var body = (BlockHir)Visit(ctx.block());

        return new FuncHir(
            Name: name,
            Parameters: @params,
            Body: body,
            Span: Span(ctx));
    }

    public override object VisitIdentifier(
        MiniLangParser.IdentifierContext ctx)
    {
        return new VarHir(
            Name: ctx
                .ID()
                .GetText(),
            Span: Span(ctx));
    }

    public override object VisitIfStmt(
        MiniLangParser.IfStmtContext ctx)
    {
        return new IfHir(
            Cond: (ExprHir)Visit(ctx.expression()),
            Then: (StmtHir)Visit(ctx.statement(0)),
            Else: ctx.ELSE() != null
                ? (StmtHir)Visit(ctx.statement(1))
                : null,
            Span: Span(ctx));
    }

    public override object VisitIntLiteral(
        MiniLangParser.IntLiteralContext ctx)
    {
        return new IntHir(
            Value: long.Parse(
                ctx
                    .INT()
                    .GetText()),
            Span: Span(ctx));
    }

    public override object VisitLogicalAnd(
        MiniLangParser.LogicalAndContext ctx)
    {
        return ctx.AND_AND()
            .Length == 0
            ? Visit(ctx.equality(0))
            : BuildLeftAssoc(
                terms: ctx.equality(),
                ops: ctx.AND_AND());
    }

    public override object VisitLogicalOr(
        MiniLangParser.LogicalOrContext ctx)
    {
        return ctx.OR_OR()
            .Length == 0
            ? Visit(ctx.logicalAnd(0))
            : BuildLeftAssoc(
                terms: ctx.logicalAnd(),
                ops: ctx.OR_OR());
    }

    public override object VisitMultiplication(
        MiniLangParser.MultiplicationContext ctx)
    {
        return ctx.children.Count == 1
            ? Visit(ctx.unary(0))
            : BuildLeftAssoc(
                terms: ctx.unary(),
                ops: ExtractOps(
                    ctx: ctx,
                    "*",
                    "/",
                    "%"));
    }

    public override object VisitParens(
        MiniLangParser.ParensContext ctx)
    {
        return Visit(ctx.expression());
    }

    public override object VisitPostfixExpr(
        MiniLangParser.PostfixExprContext ctx)
    {
        var expr = (ExprHir)Visit(ctx.primary());

        foreach (IParseTree? child in ctx.children)
        {
            switch (child)
            {
                case MiniLangParser.IndexSuffixContext idx:
                    expr = new IndexHir(
                        Target: expr,
                        Index: (ExprHir)Visit(idx.expression()),
                        Span: Span(idx));

                    break;

                case MiniLangParser.CallSuffixContext call:
                    List<ExprHir> args = call.argumentList() is null
                        ? []
                        : call
                            .argumentList()
                            .expression()
                            .Select(e => (ExprHir)Visit(e))
                            .ToList();

                    expr = new CallHir(
                        Callee: expr,
                        Args: args,
                        Span: Span(call));

                    break;
            }
        }

        return expr;
    }

    public override object VisitProgram(
        MiniLangParser.ProgramContext ctx)
    {
        List<FuncHir> funcs = ctx
            .functionDecl()
            .Select(f => (FuncHir)Visit(f))
            .ToList();

        return new ProgramHir(funcs);
    }

    public override object VisitReturnStmt(
        MiniLangParser.ReturnStmtContext ctx)
    {
        return new ReturnHir(
            Expr: ctx.expression() is null
                ? null
                : (ExprHir)Visit(ctx.expression()),
            Span: Span(ctx));
    }

    public override object VisitStatement(
        MiniLangParser.StatementContext ctx)
    {
        if (ctx.variableDecl() != null)
        {
            return Visit(ctx.variableDecl());
        }

        if (ctx.ifStmt() != null)
        {
            return Visit(ctx.ifStmt());
        }

        if (ctx.whileStmt() != null)
        {
            return Visit(ctx.whileStmt());
        }

        if (ctx.forStmt() != null)
        {
            return Visit(ctx.forStmt());
        }

        if (ctx.breakStmt() != null)
        {
            return new BreakHir(Span(ctx));
        }

        if (ctx.continueStmt() != null)
        {
            return new ContinueHir(Span(ctx));
        }

        if (ctx.returnStmt() != null)
        {
            return Visit(ctx.returnStmt());
        }

        if (ctx.block() != null)
        {
            return Visit(ctx.block());
        }

        if (ctx.exprStmt() != null)
        {
            return Visit(ctx.exprStmt());
        }

        throw new ArgumentException("Unknown statement variant");
    }

    public override object VisitStringLiteral(
        MiniLangParser.StringLiteralContext ctx)
    {
        return new StringHir(
            Value: UnescapeString(
                ctx
                    .STRING()
                    .GetText()),
            Span: Span(ctx));
    }

    public override object VisitUnary(
        MiniLangParser.UnaryContext ctx)
    {
        return ctx.unary() is null
            ? Visit(ctx.postfixExpr())
            : new UnHir(
                Op: UnMap[ctx
                    .children[0]
                    .GetText()],
                Operand: (ExprHir)Visit(ctx.unary()),
                Span: Span(ctx));
    }

    public override object VisitVariableDecl(
        MiniLangParser.VariableDeclContext ctx)
    {
        string? id = ctx
            .ID()
            .GetText();

        ExprHir? init = ctx.expression() is null
            ? null
            : (ExprHir)Visit(ctx.expression());

        return new LetHir(
            Name: id,
            Init: init,
            Span: Span(ctx));
    }

    public override object VisitWhileStmt(
        MiniLangParser.WhileStmtContext ctx)
    {
        return new WhileHir(
            Cond: (ExprHir)Visit(ctx.expression()),
            Body: (StmtHir)Visit(ctx.statement()),
            Span: Span(ctx));
    }

    private ExprHir BuildLeftAssoc<T>(
        IReadOnlyList<T> terms,
        IReadOnlyList<ITerminalNode> ops)
        where T : ParserRuleContext
    {
        var e = (ExprHir)Visit(terms[0]);

        for (int i = 0; i < ops.Count; i++)
        {
            BinOp op = BinMap[ops[i]
                .GetText()];

            e = new BinHir(
                Op: op,
                Left: e,
                Right: (ExprHir)Visit(terms[i + 1]),
                Span: Span(terms[i]));
        }

        return e;
    }

    private BlockHir ExprListToBlock(
        MiniLangParser.ExpressionListContext listCtx)
    {
        List<StmtHir> stmts = listCtx
            .expression()
            .Select(e => (StmtHir)new ExprStmtHir(
                Expr: (ExprHir)Visit(e),
                Span: Span(e)))
            .ToList();

        return new BlockHir(
            Statements: stmts,
            Span: Span(listCtx));
    }

    private static List<ITerminalNode> ExtractOps(
        ParserRuleContext ctx,
        params string[] targets)
    {
        var list = new List<ITerminalNode>();

        foreach (IParseTree? c in ctx.children)
        {
            if (c is ITerminalNode t && Array.Exists(
                    array: targets,
                    match: s => s == t.Symbol.Text))
            {
                list.Add(t);
            }
        }

        return list;
    }
    private static SourceSpan Span(
        ParserRuleContext ctx)
    {
        return new SourceSpan(
            StartLine: ctx.Start.Line,
            StartCol: ctx.Start.Column,
            EndLine: ctx.Stop.Line,
            EndCol: ctx.Stop.Column);
    }

    private static char UnescapeChar(
        string s)
    {
        if (s[1] != '\\')
        {
            return s[1];
        }

        return s[2] switch { 'n' => '\n', 'r' => '\r', 't' => '\t', '\\' => '\\', '\'' => '\'', '"' => '"', _ => s[2] };
    }

    private static string UnescapeString(
        string s)
    {
        return s
            .Substring(
                startIndex: 1,
                length: s.Length - 2)
            .Replace(
                oldValue: "\\\"",
                newValue: "\"")
            .Replace(
                oldValue: "\\\\",
                newValue: "\\");
    }
}
