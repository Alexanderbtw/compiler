using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

using Compiler.Frontend.AST;
using Compiler.Frontend.AST.Expressions;
using Compiler.Frontend.AST.Statements;
using Compiler.Frontend.Interpretation.Exceptions;
using Compiler.Frontend.Interpretation.Signals;
using Compiler.Frontend.Metadata;
using Compiler.Frontend.Semantic.Exceptions;

namespace Compiler.Frontend.Interpretation;

/// <summary>Tree-walking interpreter for MiniLang.</summary>
/// <summary>
///     Interpreter for our AST-based language. Supports integers, booleans,
///     strings, arrays, variables, control flow (if/while/for), function calls,
///     built-ins, and return signals.
/// </summary>
public class Interpreter
{
    private readonly Dictionary<string, FuncDef> _funcs;
    private readonly Stack<Frame> _stack = new();

    public Interpreter(ProgramAst prog)
    {
        _funcs = prog.Functions.ToDictionary(f => f.Name);
        if (!_funcs.ContainsKey("main"))
            throw new SemanticException("entry function 'main' not found");
    }

    private static object? ApplyBinary(string op, object? l, object? r) => op switch
    {
        "+" => ToLong(l) + ToLong(r),
        "-" => ToLong(l) - ToLong(r),
        "*" => ToLong(l) * ToLong(r),
        "/" => ToLong(l) / ToLong(r),
        "%" => ToLong(l) % ToLong(r),

        "<" => ToLong(l) < ToLong(r),
        "<=" => ToLong(l) <= ToLong(r),
        ">" => ToLong(l) > ToLong(r),
        ">=" => ToLong(l) >= ToLong(r),

        "==" => Equals(l, r),
        "!=" => !Equals(l, r),

        // Fixed: coerce both sides to bool
        "&&" => IsTrue(l) && IsTrue(r),
        "||" => IsTrue(l) || IsTrue(r),

        _ => throw new RuntimeException($"binary op '{op}' not implemented")
    };

    private static object? ApplyUnary(string op, object? r) => op switch
    {
        "-" => -ToLong(r),
        "!" => !IsTrue(r),
        _ => throw new RuntimeException($"unknown unary op '{op}'")
    };

    private static object? ArrayGet(object? arrObj, int index)
    {
        if (arrObj is not Array arr)
            throw new RuntimeException("indexing a non-array value");
        if (index < 0 || index >= arr.Length)
            throw new RuntimeException("array index out of bounds");
        return arr.GetValue(index);
    }

    private static void ArraySet(object? arrObj, int index, object? value)
    {
        if (arrObj is not Array arr)
            throw new RuntimeException("indexing a non-array value");
        if (index < 0 || index >= arr.Length)
            throw new RuntimeException("array index out of bounds");
        arr.SetValue(value, index);
    }

    private object? Call(string name, object?[] args)
    {
        // 1. Built-ins
        if (Builtins.TryInvoke(name, args, out object? builtin))
            return builtin;

        // 2. User function
        if (!_funcs.TryGetValue(name, out FuncDef? f))
            throw new RuntimeException($"undefined function '{name}'");
        if (f.Params.Count != args.Length)
            throw new RuntimeException(
                $"call to '{name}' expects {f.Params.Count} args, got {args.Length}");

        var frame = new Frame();
        for (var i = 0; i < args.Length; i++)
            frame.Locals[f.Params[i]] = args[i];

        _stack.Push(frame);
        try
        {
            ExecBlock(f.Body);
        }
        catch (ReturnSignal ret)
        {
            _stack.Pop();
            return ret.Value;
        }
        _stack.Pop();
        return null;
    }

    private object? Eval(Expr? e) => e switch
    {
        null => null,
        IntLit i => i.Value,
        CharLit c => c.Value,
        StringLit s => s.Value,
        BoolLit b => b.Value,
        VarExpr v => Lookup(v.Name),
        UnExpr u => ApplyUnary(u.Op, Eval(u.R)),
        BinExpr { Op: "=" } a => EvalAssign(a),
        BinExpr b => ApplyBinary(b.Op, Eval(b.L), Eval(b.R)),
        CallExpr c => Call(
            ((VarExpr)c.Callee).Name,
            c.A.Select(arg => Eval(arg)).ToArray()),
        IndexExpr ix => ArrayGet(
            Eval(ix.Arr),
            (int)ToLong(Eval(ix.Index))),
        _ => throw new RuntimeException($"expr {e.GetType().Name} not implemented")
    };

    private object? EvalAssign(BinExpr a)
    {
        object? value = Eval(a.R);
        switch (a.L)
        {
            case VarExpr v:
                SetVar(v.Name, value);
                return value;
            case IndexExpr ix:
                ArraySet(
                    Eval(ix.Arr),
                    (int)ToLong(Eval(ix.Index)),
                    value);
                return value;
            default:
                throw new RuntimeException("invalid assignment target");
        }
    }

    private void ExecBlock(Block b)
    {
        foreach (Stmt s in b.Body)
            ExecStmt(s);
    }

    private void ExecOptional(Stmt? s)
    {
        if (s != null) ExecStmt(s);
    }

    private void ExecStmt(Stmt s)
    {
        switch (s)
        {
            case VarDecl v:
                _stack.Peek().Locals[v.Name] = Eval(v.Init);
                break;
            case ExprStmt e:
                if (e.E != null) Eval(e.E);
                break;
            case Return r:
                throw new ReturnSignal(Eval(r.Value));
            case IfStmt ifs:
                if (IsTrue(Eval(ifs.Cond))) ExecStmt(ifs.Then);
                else if (ifs.Else != null) ExecStmt(ifs.Else);
                break;
            case WhileStmt w:
                while (IsTrue(Eval(w.Cond)))
                    try { ExecStmt(w.Body); }
                    catch (ContinueSignal) { }
                    catch (BreakSignal) { break; }
                break;
            case ForStmt f:
                ExecOptional(f.Init);
                while (f.Cond == null || IsTrue(Eval(f.Cond)))
                {
                    try { ExecStmt(f.Body); }
                    catch (ContinueSignal) { }
                    catch (BreakSignal) { break; }

                    if (f.Iter != null)
                        foreach (Expr iter in f.Iter)
                            Eval(iter);
                }
                break;
            case Break:
                throw new BreakSignal();
            case Continue:
                throw new ContinueSignal();
            case Block b:
                ExecBlock(b);
                break;
            default:
                throw new RuntimeException($"stmt {s.GetType().Name} not implemented");
        }
    }

    private static bool IsTrue(object? v) => v switch
    {
        bool b => b,
        long n => n != 0,
        _ => v != null
    };

    private object? Lookup(string name)
    {
        foreach (Frame frame in _stack)
            if (frame.Locals.TryGetValue(name, out object? v))
                return v;
        throw new RuntimeException($"unbound variable '{name}'");
    }

    public object? Run(bool withTiming = false)
    {
        Stopwatch? sw = withTiming ? Stopwatch.StartNew() : null;
        object? result = Call("main", []);
        if (withTiming)
            Console.WriteLine($"⏱ {sw!.ElapsedMilliseconds} ms");
        return result;
    }

    private void SetVar(string name, object? val)
    {
        foreach (Frame frame in _stack)
            if (frame.Locals.ContainsKey(name))
            {
                frame.Locals[name] = val;
                return;
            }
        throw new RuntimeException($"unbound variable '{name}'");
    }

    private static long ToLong(object? v) => v switch
    {
        long n => n,
        bool b => b ? 1 : 0,
        null => throw new RuntimeException("null used where integer expected"),
        _ => throw new RuntimeException($"cannot use {v.GetType().Name} in arithmetic")
    };

    private sealed class Frame
    {
        public readonly Dictionary<string, object?> Locals = new();
    }
}
