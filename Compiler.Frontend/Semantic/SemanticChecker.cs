using System;
using System.Collections.Generic;
using System.Linq;

using Compiler.Frontend.AST;
using Compiler.Frontend.AST.Expressions;
using Compiler.Frontend.AST.Statements;
using Compiler.Frontend.Semantic.Exceptions;
using Compiler.Frontend.Semantic.Symbols;
using Compiler.Frontend.Services;

namespace Compiler.Frontend.Semantic;

public sealed class SemanticChecker
{
    private FuncDef? _currentFunc;

    private readonly List<string> _errors = new();

    private readonly Dictionary<string, FuncSymbol> _global = new();
    private int _loops;
    private readonly Stack<Dictionary<string, Symbol>> _scopes = new();

    private bool Add(string name, Symbol s, string dupMsg)
    {
        Dictionary<string, Symbol> table = _scopes.Peek();
        if (!table.TryAdd(name, s))
        {
            Error(dupMsg);
            return false;
        }
        return true;
    }
    private void PopulateGlobalWithBuiltins()
    {
        foreach (var kv in Builtins.Table)
            _global[kv.Key] = new FuncSymbol(kv.Value.Name,
                Enumerable.Repeat("_", Math.Max(0, kv.Value.Arity)).ToList());
    }

    public void Check(ProgramAst prog)
    {
        _global.Clear();
        PopulateGlobalWithBuiltins();
        _loops = 0;
        _errors.Clear();

        foreach (FuncDef f in prog.Functions)
            DeclareFunc(f);

        foreach (FuncDef f in prog.Functions)
        {
            _currentFunc = f;
            PushScope();
            foreach (string p in f.Params)
                Add(p, new ParamSymbol(p), $"parameter '{p}' already defined in '{f.Name}'");
            VisitBlock(f.Body);
            PopScope();
        }

        if (_errors.Count > 0) throw new SemanticException(string.Join('\n', _errors));
    }

    private void DeclareFunc(FuncDef f)
    {
        if (_global.ContainsKey(f.Name))
            Error($"function '{f.Name}' already declared");
        else
            _global[f.Name] = new FuncSymbol(f.Name, f.Params);
    }
    private void Error(string msg) => _errors.Add(msg);
    private void PopScope() => _scopes.Pop();

    private void PushScope() => _scopes.Push(new Dictionary<string, Symbol>());

    private Symbol? Resolve(string name)
    {
        foreach (Dictionary<string, Symbol> s in _scopes)
            if (s.TryGetValue(name, out Symbol? sym))
                return sym;
        return _global.GetValueOrDefault(name);
    }

    private void VisitBlock(Block b)
    {
        foreach (Stmt s in b.Body) VisitStmt(s);
    }

    private void VisitCall(CallExpr c)
    {
        if (c.Callee is not VarExpr callee)
        {
            Error("only simple function names are callable");
            return;
        }

        bool isBuiltin = Builtins.Exists(callee.Name);
        if (isBuiltin)
        {
            int arity = Builtins.GetArity(callee.Name);
            if (arity >= 0 && arity != c.A.Count)
                Error($"call to '{callee.Name}' expects {arity} args, got {c.A.Count}");
        }
        else
        {
            Symbol? sym = Resolve(callee.Name);
            if (sym is not FuncSymbol fs)
            {
                Error($"'{callee.Name}' is not a function");
                return;
            }

            if (fs.Parameters.Count != c.A.Count)
                Error($"call to '{fs.Name}' expects {fs.Parameters.Count} args, got {c.A.Count}");
        }

        foreach (var a in c.A) VisitExpr(a);
    }

    private void VisitExpr(Expr e)
    {
        switch (e)
        {
            case IntLit or StringLit or CharLit or BoolLit:
                return;

            case VarExpr v:
                if (Resolve(v.Name) is null)
                    Error($"identifier '{v.Name}' not in scope");
                return;

            case BinExpr b:
                VisitExpr(b.L);
                VisitExpr(b.R);
                return;

            case UnExpr u:
                VisitExpr(u.R);
                return;

            case CallExpr c:
                VisitCall(c);
                return;

            case IndexExpr ix:
                VisitExpr(ix.Arr);
                VisitExpr(ix.Index);
                return;
        }
    }

    private void VisitStmt(Stmt s)
    {
        switch (s)
        {
            case VarDecl vd: VisitVarDecl(vd); break;
            case ExprStmt es:
                if (es.E != null) VisitExpr(es.E);
                break;

            case IfStmt ifs:
                VisitExpr(ifs.Cond);
                VisitStmt(ifs.Then);
                if (ifs.Else != null) VisitStmt(ifs.Else);
                break;

            case WhileStmt ws:
                _loops++;
                VisitExpr(ws.Cond);
                VisitStmt(ws.Body);
                _loops--;
                break;

            case ForStmt fs:
                _loops++;
                if (fs.Init != null) VisitStmt(fs.Init);
                if (fs.Cond != null) VisitExpr(fs.Cond);
                if (fs.Iter != null)
                    foreach (Expr e in fs.Iter)
                        VisitExpr(e);
                VisitStmt(fs.Body);
                _loops--;
                break;

            case Break:
            case Continue:
                if (_loops == 0) Error($"'{s.GetType().Name.ToLower()}' used outside loop");
                break;

            case Return r:
                if (_currentFunc == null) break;
                if (r.Value != null) VisitExpr(r.Value);
                break;

            case Block blk: VisitBlock(blk); break;
        }
    }

    private void VisitVarDecl(VarDecl vd)
    {
        if (Add(vd.Name, new VarSymbol(vd.Name), $"variable '{vd.Name}' already defined"))
            if (vd.Init != null)
                VisitExpr(vd.Init);
    }
}
