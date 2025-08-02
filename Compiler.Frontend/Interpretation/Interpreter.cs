using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

using Compiler.Frontend.AST;
using Compiler.Frontend.AST.Expressions;
using Compiler.Frontend.AST.Statements;
using Compiler.Frontend.Interpretation.Contracts;
using Compiler.Frontend.Interpretation.Exceptions;
using Compiler.Frontend.Semantic;

namespace Compiler.Frontend.Interpretation;

/// <summary>Tree-walking interpreter for MiniLang.</summary>
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
        "+" => (long)l! + (long)r!,
        "-" => (long)l! - (long)r!,
        "*" => (long)l! * (long)r!,
        "/" => (long)l! / (long)r!,
        "%" => (long)l! % (long)r!,

        "<" => (long)l! < (long)r!,
        "<=" => (long)l! <= (long)r!,
        ">" => (long)l! > (long)r!,
        ">=" => (long)l! >= (long)r!,

        "==" => Equals(l, r),
        "!=" => !Equals(l, r),

        "&&" => IsTrue(l) ? r : false,
        "||" => IsTrue(l) ? true : r,

        "=" => throw new RuntimeException("assignment handled at stmt level"),

        _ => throw new RuntimeException($"binary op '{op}' not implemented")
    };

    private static object? ApplyUnary(string op, object? r) => op switch
    {
        "-" => -(long)r!,
        "!" => !IsTrue(r),
        _ => throw new RuntimeException($"unknown unary op '{op}'")
    };

    private object? Call(string name, object?[] args)
    {
        // 1) built-ins
        if (Builtins.TryInvoke(name, args, out object? builtin)) return builtin;

        // 2) user function
        if (!_funcs.TryGetValue(name, out FuncDef? f))
            throw new RuntimeException($"undefined function '{name}'");

        if (f.Params.Count != args.Length)
            throw new RuntimeException(
                $"call to '{name}' expects {f.Params.Count} args, got {args.Length}");

        var frame = new Frame();
        for (var i = 0; i < args.Length; i++)
            frame.Locals[f.Params[i]] = args[i];

        _stack.Push(frame);
        try { ExecBlock(f.Body); }
        catch (ReturnSignal r)
        {
            _stack.Pop();
            return r.Value;
        }
        _stack.Pop();
        return null;
    }

    private object? Eval(Expr? e)
    {
        return e switch
        {
            null => null,

            IntLit i => i.Value,
            CharLit c => c.Value,
            StringLit s => s.Value,
            BoolLit b => b.Value,

            VarExpr v => Lookup(v.Name),

            UnExpr u => ApplyUnary(u.Op, Eval(u.R)),

            BinExpr b => ApplyBinary(b.Op, Eval(b.L), Eval(b.R)),

            CallExpr c => Call(
                ((VarExpr)c.Callee).Name,
                c.A.Select(a => Eval(a)).ToArray()),

            IndexExpr ix => () =>
            {
                var arr = (object?[])Eval(ix.Arr)!;
                var idx = Convert.ToInt32(Eval(ix.Index));
                return arr[idx];
            },

            _ => throw new RuntimeException($"expr {e.GetType().Name} not implemented")
        };
    }

    private void ExecBlock(Block b)
    {
        foreach (Stmt s in b.Body)
            ExecStmt(s);
    }

    private static void ExecOptional(Stmt? s)
    {
        if (s != null) ExecOptional(s);
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
                {
                    try { ExecStmt(w.Body); }
                    catch (ContinueSignal)
                    {
                    }
                    catch (BreakSignal) { break; }
                }
                break;

            case ForStmt f:
                ExecOptional(f.Init);
                while (f.Cond == null || IsTrue(Eval(f.Cond)))
                {
                    try
                    {
                        ExecStmt(f.Body);
                    }
                    catch (ContinueSignal)
                    {
                        /* fall through */
                    }
                    catch (BreakSignal) { break; }

                    if (f.Iter != null)
                        foreach (Expr iter in f.Iter)
                            Eval(iter);
                }
                break;

            case Break: throw new BreakSignal();
            case Continue: throw new ContinueSignal();

            case Block b: ExecBlock(b); break;

            default: throw new RuntimeException($"stmt {s.GetType().Name} not implemented");
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
        foreach (Frame frame in _stack) // from innermost to outer
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

    private sealed class Frame
    {
        public readonly Dictionary<string, object?> Locals = new();
    }
}

