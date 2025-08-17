using Compiler.Translation.HIR.Common;
using Compiler.Translation.HIR.Expressions;
using Compiler.Translation.HIR.Expressions.Abstractions;
using Compiler.Translation.HIR.Metadata;
using Compiler.Translation.HIR.Semantic.Exceptions;
using Compiler.Translation.HIR.Semantic.Symbols;
using Compiler.Translation.HIR.Statements;
using Compiler.Translation.HIR.Statements.Abstractions;
using Compiler.Translation.HIR.Stringify;

namespace Compiler.Translation.HIR.Semantic;

public sealed class SemanticChecker
{
    private FuncHir? _currentFunc;

    private readonly List<string> _errors = new();

    private readonly Dictionary<string, FuncSymbol> _funcs = new();
    private int _loops;
    private readonly Stack<Dictionary<string, Symbol>> _values = new();

    public void Check(ProgramHir prog)
    {
        _funcs.Clear();
        PopulateGlobalWithBuiltins();
        _loops = 0;
        _errors.Clear();

        foreach (FuncHir f in prog.Functions)
            DeclareFunc(f);

        foreach (FuncHir f in prog.Functions)
        {
            _currentFunc = f;
            PushScope();
            foreach (string p in f.Parameters)
                AddValue(p, new ParamSymbol(p), $"parameter '{p}' already defined in '{f.Name}'");
            VisitBlock(f.Body);
            PopScope();
        }

        if (_errors.Count > 0) throw new SemanticException(string.Join('\n', _errors));
    }

    private bool AddValue(string name, Symbol s, string dupMsg)
    {
        Dictionary<string, Symbol> table = _values.Peek();
        if (!table.TryAdd(name, s))
        {
            Error(dupMsg);
            return false;
        }
        return true;
    }

    private void DeclareFunc(FuncHir f)
    {
        if (_funcs.ContainsKey(f.Name))
            Error($"function '{f.Name}' already declared");
        else
            _funcs[f.Name] = new FuncSymbol(f.Name, f.Parameters);
    }

    private void Error(string msg) => _errors.Add(msg);
    private void Error(SourceSpan span, string msg) => _errors.Add($"[{span}] {msg}");
    private void PopScope() => _values.Pop();

    private void PopulateGlobalWithBuiltins()
    {
        // Register builtin function names in the function namespace to prevent user re-declaration.
        // We don't rely on parameter counts here because arity for builtins is checked separately
        // via descriptors (supports overloads and varargs).
        foreach (var kv in Builtins.Table)
            _funcs[kv.Key] = new FuncSymbol(kv.Key, Array.Empty<string>());
    }

    private void CheckBuiltinArity(string name, int argCount, SourceSpan span)
    {
        var cands = Builtins.GetCandidates(name);
        if (cands.Count == 0) return; // not a builtin — should not happen if Exists returned true

        bool ok = false;
        foreach (var d in cands)
        {
            int min = d.MinArity;
            int max = d.MaxArity ?? d.MinArity;
            if (d.Attributes.HasFlag(BuiltinAttr.VarArgs))
            {
                if (argCount >= min) { ok = true; break; }
            }
            else
            {
                if (argCount >= min && argCount <= max) { ok = true; break; }
            }
        }

        if (!ok)
        {
            var expected = string.Join(" | ", cands.Select(d =>
                d.Attributes.HasFlag(BuiltinAttr.VarArgs)
                    ? $"{d.MinArity}+"
                    : (d.MaxArity is int mx && mx != d.MinArity ? $"{d.MinArity}..{mx}" : $"{d.MinArity}")));
            Error(span, $"call to '{name}' has {argCount} args; expected {expected}");
        }
    }

    private void PushScope() => _values.Push(new Dictionary<string, Symbol>());

    private Symbol? ResolveValue(string name)
    {
        foreach (Dictionary<string, Symbol> s in _values)
            if (s.TryGetValue(name, out Symbol? sym))
                return sym;
        return null;
    }

    private void VisitBlock(BlockHir b)
    {
        PushScope();
        foreach (StmtHir s in b.Statements) VisitStmt(s);
        PopScope();
    }

    private void VisitCall(CallHir c)
    {
        if (c.Callee is not VarHir callee)
        {
            Error(c.Span, "only simple function names are callable");
            return;
        }

        bool isBuiltin = Builtins.Exists(callee.Name);
        if (isBuiltin)
        {
            CheckBuiltinArity(callee.Name, c.Args.Count, c.Span);
        }
        else
        {
            FuncSymbol? fsym = _funcs.GetValueOrDefault(callee.Name);
            if (fsym is null)
            {
                Error(c.Span, $"'{callee.Name}' is not a function");
                return;
            }

            if (fsym.Parameters.Count != c.Args.Count)
                Error(c.Span, $"call to '{fsym.Name}' expects {fsym.Parameters.Count} args, got {c.Args.Count}");
        }

        foreach (ExprHir a in c.Args) VisitExpr(a);
    }

    private void VisitExpr(ExprHir e)
    {
        switch (e)
        {
            case IntHir or StringHir or CharHir or BoolHir:
                return;

            case VarHir v:
            {
                if (ResolveValue(v.Name) is null)
                    Error(v.Span, $"identifier '{v.Name}' not in scope");
                return;
            }

            case BinHir b:
                if (b.Op == BinOp.Assign && b.Left is not VarHir && b.Left is not IndexHir)
                    Error(b.Span, "left-hand side of assignment is not assignable");
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

    private void VisitLet(LetHir vd)
    {
        if (AddValue(vd.Name, new VarSymbol(vd.Name), $"variable '{vd.Name}' already defined"))
            if (vd.Init != null)
                VisitExpr(vd.Init);
    }

    private void VisitStmt(StmtHir s)
    {
        switch (s)
        {
            case LetHir vd: VisitLet(vd); break;

            case ExprStmtHir es:
                if (es.Expr != null) VisitExpr(es.Expr);
                break;

            case IfHir ifs:
                VisitExpr(ifs.Cond);
                VisitStmt(ifs.Then);
                if (ifs.Else != null) VisitStmt(ifs.Else);
                break;

            case WhileHir ws:
                _loops++;
                VisitExpr(ws.Cond);
                VisitStmt(ws.Body);
                _loops--;
                break;

            case BreakHir br:
                if (_loops == 0) Error(br.Span, "'break' used outside loop");
                break;

            case ContinueHir cont:
                if (_loops == 0) Error(cont.Span, "'continue' used outside loop");
                break;

            case ReturnHir r:
                if (_currentFunc == null)
                {
                    Error(r.Span, "'return' used outside of function");
                    break;
                }
                if (r.Expr != null) VisitExpr(r.Expr);
                break;

            case BlockHir blk:
                VisitBlock(blk);
                break;
        }
    }
}
