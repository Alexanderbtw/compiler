using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

using Compiler.Interpreter.Exceptions;
using Compiler.Interpreter.Signals;
using Compiler.Translation.HIR.Common;
using Compiler.Translation.HIR.Expressions;
using Compiler.Translation.HIR.Expressions.Abstractions;
using Compiler.Translation.HIR.Statements;
using Compiler.Translation.HIR.Statements.Abstractions;
using Compiler.Translation.Metadata;
using Compiler.Translation.Semantic.Exceptions;

namespace Compiler.Interpreter;

/// <summary>Tree-walking interpreter for MiniLang over HIR.</summary>
public class Interpreter
{
    private readonly Dictionary<string, FuncHir> _funcs;
    private readonly Stack<Frame> _stack = new();

    public Interpreter(ProgramHir prog)
    {
        _funcs = prog.Functions.ToDictionary(f => f.Name);
        if (!_funcs.ContainsKey("main"))
            throw new SemanticException("entry function 'main' not found");
    }

    private static object? ApplyBinary(BinOp op, object? l, object? r) => op switch
    {
        BinOp.Add => ToLong(l) + ToLong(r),
        BinOp.Sub => ToLong(l) - ToLong(r),
        BinOp.Mul => ToLong(l) * ToLong(r),
        BinOp.Div => ToLong(l) / ToLong(r),
        BinOp.Mod => ToLong(l) % ToLong(r),

        BinOp.Lt => ToLong(l) < ToLong(r),
        BinOp.Le => ToLong(l) <= ToLong(r),
        BinOp.Gt => ToLong(l) > ToLong(r),
        BinOp.Ge => ToLong(l) >= ToLong(r),

        BinOp.Eq => Equals(l, r),
        BinOp.Ne => !Equals(l, r),

        BinOp.And or BinOp.Or => throw new RuntimeException("internal: &&/|| should be handled in Eval"),
        BinOp.Assign => throw new RuntimeException("internal: '=' handled in EvalAssign"),

        _ => throw new RuntimeException($"binary op '{op}' not implemented")
    };

    private static object? ApplyUnary(UnOp op, object? r) => op switch
    {
        UnOp.Neg => -ToLong(r),
        UnOp.Not => !IsTrue(r),
        UnOp.Plus => ToLong(r),
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
        if (Builtins.TryInvoke(name, args, out object? builtin))
            return builtin;

        if (!_funcs.TryGetValue(name, out FuncHir? f))
            throw new RuntimeException($"undefined function '{name}'");
        if (f.Parameters.Count != args.Length)
            throw new RuntimeException(
                $"call to '{name}' expects {f.Parameters.Count} args, got {args.Length}");

        var frame = new Frame();
        for (var i = 0; i < args.Length; i++)
            frame.Locals[f.Parameters[i]] = args[i];

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

    private object? Eval(ExprHir? e) => e switch
    {
        null => null,
        IntHir i => i.Value,
        CharHir c => c.Value,
        StringHir s => s.Value,
        BoolHir b => b.Value,
        VarHir v => Lookup(v.Name),
        UnHir u => ApplyUnary(u.Op, Eval(u.Operand)),
        BinHir { Op: BinOp.Assign } a => EvalAssign(a),
        BinHir { Op: BinOp.And } b => IsTrue(Eval(b.Left)) && IsTrue(Eval(b.Right)),
        BinHir { Op: BinOp.Or } b2 => IsTrue(Eval(b2.Left)) || IsTrue(Eval(b2.Right)),
        BinHir b3 => ApplyBinary(b3.Op, Eval(b3.Left), Eval(b3.Right)),
        CallHir c => Call((c.Callee as VarHir)?.Name
                          ?? throw new RuntimeException("only simple function names are callable"),
                         c.Args.Select(Eval).ToArray()),
        IndexHir ix => ArrayGet(Eval(ix.Target), (int)ToLong(Eval(ix.Index))),
        _ => throw new RuntimeException($"expr {e.GetType().Name} not implemented")
    };

    private object? EvalAssign(BinHir a)
    {
        object? value = Eval(a.Right);
        switch (a.Left)
        {
            case VarHir v:
                SetVar(v.Name, value);
                return value;
            case IndexHir ix:
                ArraySet(
                    Eval(ix.Target),
                    (int)ToLong(Eval(ix.Index)),
                    value);
                return value;
            default:
                throw new RuntimeException("invalid assignment target");
        }
    }

    private void ExecBlock(BlockHir b)
    {
        foreach (StmtHir s in b.Statements)
            ExecStmt(s);
    }

    private void ExecOptional(StmtHir? s)
    {
        if (s != null) ExecStmt(s);
    }

    private void ExecStmt(StmtHir s)
    {
        switch (s)
        {
            case LetHir v:
                _stack.Peek().Locals[v.Name] = Eval(v.Init);
                break;
            case ExprStmtHir e:
                if (e.Expr != null) Eval(e.Expr);
                break;
            case ReturnHir r:
                throw new ReturnSignal(Eval(r.Expr));
            case IfHir ifs:
                if (IsTrue(Eval(ifs.Cond))) ExecStmt(ifs.Then);
                else if (ifs.Else != null) ExecStmt(ifs.Else);
                break;
            case WhileHir w:
                while (IsTrue(Eval(w.Cond)))
                    try { ExecStmt(w.Body); }
                    catch (ContinueSignal) { }
                    catch (BreakSignal) { break; }
                break;
            case BreakHir:
                throw new BreakSignal();
            case ContinueHir:
                throw new ContinueSignal();
            case BlockHir b:
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