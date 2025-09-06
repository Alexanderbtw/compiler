using Compiler.Frontend.Translation.HIR.Common;
using Compiler.Frontend.Translation.HIR.Expressions;
using Compiler.Frontend.Translation.HIR.Expressions.Abstractions;
using Compiler.Frontend.Translation.HIR.Metadata;
using Compiler.Frontend.Translation.HIR.Semantic.Exceptions;
using Compiler.Frontend.Translation.HIR.Semantic.Symbols;
using Compiler.Frontend.Translation.HIR.Statements;
using Compiler.Frontend.Translation.HIR.Statements.Abstractions;
using Compiler.Frontend.Translation.HIR.Stringify;

namespace Compiler.Frontend.Translation.HIR.Semantic;

public sealed class SemanticChecker
{
    private FuncHir? _currentFunc;

    private readonly List<string> _errors = [];

    private readonly Dictionary<string, FuncSymbol> _funcs = new Dictionary<string, FuncSymbol>();
    private int _loops;
    private readonly Stack<Dictionary<string, Symbol>> _values = new Stack<Dictionary<string, Symbol>>();

    public void Check(
        ProgramHir prog)
    {
        _funcs.Clear();
        PopulateGlobalWithBuiltins();
        _loops = 0;
        _errors.Clear();

        foreach (FuncHir f in prog.Functions)
        {
            DeclareFunc(f);
        }

        foreach (FuncHir f in prog.Functions)
        {
            _currentFunc = f;
            PushScope();

            foreach (string p in f.Parameters)
            {
                AddValue(
                    name: p,
                    s: new ParamSymbol(p),
                    dupMsg: $"parameter '{p}' already defined in '{f.Name}'");
            }

            VisitBlock(f.Body);
            PopScope();
        }

        if (_errors.Count > 0)
        {
            throw new SemanticException(
                string.Join(
                    separator: '\n',
                    values: _errors));
        }
    }

    private bool AddValue(
        string name,
        Symbol s,
        string dupMsg)
    {
        Dictionary<string, Symbol> table = _values.Peek();

        if (!table.TryAdd(
                key: name,
                value: s))
        {
            Error(dupMsg);

            return false;
        }

        return true;
    }

    private void CheckBuiltinArity(
        string name,
        int argCount,
        SourceSpan span)
    {
        IReadOnlyList<BuiltinDescriptor> cands = Builtins.GetCandidates(name);

        if (cands.Count == 0)
        {
            return; // not a builtin — should not happen if Exists returned true
        }

        bool ok = false;

        foreach (BuiltinDescriptor d in cands)
        {
            int min = d.MinArity;
            int max = d.MaxArity ?? d.MinArity;

            if (d.Attributes.HasFlag(BuiltinAttr.VarArgs))
            {
                if (argCount >= min)
                {
                    ok = true;

                    break;
                }
            }
            else
            {
                if (argCount >= min && argCount <= max)
                {
                    ok = true;

                    break;
                }
            }
        }

        if (!ok)
        {
            string expected = string.Join(
                separator: " | ",
                values: cands.Select(d =>
                    d.Attributes.HasFlag(BuiltinAttr.VarArgs)
                        ? $"{d.MinArity}+"
                        : d.MaxArity is int mx && mx != d.MinArity
                            ? $"{d.MinArity}..{mx}"
                            : $"{d.MinArity}"));

            Error(
                span: span,
                msg: $"call to '{name}' has {argCount} args; expected {expected}");
        }
    }

    private void DeclareFunc(
        FuncHir f)
    {
        if (_funcs.ContainsKey(f.Name))
        {
            Error($"function '{f.Name}' already declared");
        }
        else
        {
            _funcs[f.Name] = new FuncSymbol(
                Name: f.Name,
                Parameters: f.Parameters);
        }
    }

    private void Error(
        string msg)
    {
        _errors.Add(msg);
    }

    private void Error(
        SourceSpan span,
        string msg)
    {
        _errors.Add($"[{span}] {msg}");
    }

    private void PopScope()
    {
        _values.Pop();
    }

    private void PopulateGlobalWithBuiltins()
    {
        // Register builtin function names in the function namespace to prevent user re-declaration.
        // We don't rely on parameter counts here because arity for builtins is checked separately
        // via descriptors (supports overloads and varargs).
        foreach (KeyValuePair<string, List<BuiltinDescriptor>> kv in Builtins.Table)
        {
            _funcs[kv.Key] = new FuncSymbol(
                Name: kv.Key,
                Parameters: []);
        }
    }

    private void PushScope()
    {
        _values.Push(new Dictionary<string, Symbol>());
    }

    private Symbol? ResolveValue(
        string name)
    {
        foreach (Dictionary<string, Symbol> s in _values)
        {
            if (s.TryGetValue(
                    key: name,
                    value: out Symbol? sym))
            {
                return sym;
            }
        }

        return null;
    }

    private void VisitBlock(
        BlockHir b)
    {
        PushScope();

        foreach (StmtHir s in b.Statements)
        {
            VisitStmt(s);
        }

        PopScope();
    }

    private void VisitCall(
        CallHir c)
    {
        if (c.Callee is not VarHir callee)
        {
            Error(
                span: c.Span,
                msg: "only simple function names are callable");

            return;
        }

        bool isBuiltin = Builtins.Exists(callee.Name);

        if (isBuiltin)
        {
            CheckBuiltinArity(
                name: callee.Name,
                argCount: c.Args.Count,
                span: c.Span);
        }
        else
        {
            FuncSymbol? fsym = _funcs.GetValueOrDefault(callee.Name);

            if (fsym is null)
            {
                Error(
                    span: c.Span,
                    msg: $"'{callee.Name}' is not a function");

                return;
            }

            if (fsym.Parameters.Count != c.Args.Count)
            {
                Error(
                    span: c.Span,
                    msg: $"call to '{fsym.Name}' expects {fsym.Parameters.Count} args, got {c.Args.Count}");
            }
        }

        foreach (ExprHir a in c.Args)
        {
            VisitExpr(a);
        }
    }

    private void VisitExpr(
        ExprHir e)
    {
        switch (e)
        {
            case IntHir or StringHir or CharHir or BoolHir:
                return;

            case VarHir v:
                {
                    if (ResolveValue(v.Name) is null)
                    {
                        Error(
                            span: v.Span,
                            msg: $"identifier '{v.Name}' not in scope");
                    }

                    return;
                }

            case BinHir b:
                if (b is
                    {
                        Op: BinOp.Assign,
                        Left: not VarHir and not IndexHir
                    })
                {
                    Error(
                        span: b.Span,
                        msg: "left-hand side of assignment is not assignable");
                }

                VisitExpr(b.Left);
                VisitExpr(b.Right);

                return;

            case UnHir u:
                VisitExpr(u.Operand);

                return;

            case CallHir c:
                VisitCall(c);

                return;

            case IndexHir ix:
                VisitExpr(ix.Target);
                VisitExpr(ix.Index);

                return;
        }
    }

    private void VisitLet(
        LetHir vd)
    {
        if (AddValue(
                name: vd.Name,
                s: new VarSymbol(vd.Name),
                dupMsg: $"variable '{vd.Name}' already defined"))
        {
            if (vd.Init != null)
            {
                VisitExpr(vd.Init);
            }
        }
    }

    private void VisitStmt(
        StmtHir s)
    {
        switch (s)
        {
            case LetHir vd:
                VisitLet(vd);

                break;

            case ExprStmtHir es:
                if (es.Expr != null)
                {
                    VisitExpr(es.Expr);
                }

                break;

            case IfHir ifs:
                VisitExpr(ifs.Cond);
                VisitStmt(ifs.Then);

                if (ifs.Else != null)
                {
                    VisitStmt(ifs.Else);
                }

                break;

            case WhileHir ws:
                _loops++;
                VisitExpr(ws.Cond);
                VisitStmt(ws.Body);
                _loops--;

                break;

            case BreakHir br:
                if (_loops == 0)
                {
                    Error(
                        span: br.Span,
                        msg: "'break' used outside loop");
                }

                break;

            case ContinueHir cont:
                if (_loops == 0)
                {
                    Error(
                        span: cont.Span,
                        msg: "'continue' used outside loop");
                }

                break;

            case ReturnHir r:
                if (_currentFunc == null)
                {
                    Error(
                        span: r.Span,
                        msg: "'return' used outside of function");

                    break;
                }

                if (r.Expr != null)
                {
                    VisitExpr(r.Expr);
                }

                break;

            case BlockHir blk:
                VisitBlock(blk);

                break;
        }
    }
}
