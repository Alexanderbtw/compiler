using System.Diagnostics;

using Compiler.Frontend.Translation.HIR.Common;
using Compiler.Frontend.Translation.HIR.Expressions;
using Compiler.Frontend.Translation.HIR.Expressions.Abstractions;
using Compiler.Frontend.Translation.HIR.Semantic.Exceptions;
using Compiler.Frontend.Translation.HIR.Statements;
using Compiler.Frontend.Translation.HIR.Statements.Abstractions;
using Compiler.Interpreter.Exceptions;
using Compiler.Interpreter.Signals;

namespace Compiler.Interpreter;

/// <summary>Tree-walking interpreter for MiniLang over HIR.</summary>
public sealed class Interpreter
{
    private readonly Dictionary<string, FuncHir> _funcs;
    private readonly Stack<Frame> _stack = new Stack<Frame>();

    public Interpreter(
        ProgramHir prog)
    {
        _funcs = prog.Functions.ToDictionary(f => f.Name);

        if (!_funcs.ContainsKey("main"))
        {
            throw new SemanticException("entry function 'main' not found");
        }
    }

    public object? Run(
        bool withTiming = false)
    {
        Stopwatch? sw = withTiming
            ? Stopwatch.StartNew()
            : null;

        object? result = Call(
            name: "main",
            args: []);

        if (withTiming)
        {
            Console.WriteLine($"⏱ {sw!.ElapsedMilliseconds} ms");
        }

        return result;
    }

    private static object? ApplyBinary(
        BinOp op,
        object? l,
        object? r)
    {
        return op switch
        {
            BinOp.Add => InterpreterValueOps.ToLong(l) + InterpreterValueOps.ToLong(r),
            BinOp.Sub => InterpreterValueOps.ToLong(l) - InterpreterValueOps.ToLong(r),
            BinOp.Mul => InterpreterValueOps.ToLong(l) * InterpreterValueOps.ToLong(r),
            BinOp.Div => InterpreterValueOps.ToLong(l) / InterpreterValueOps.ToLong(r),
            BinOp.Mod => InterpreterValueOps.ToLong(l) % InterpreterValueOps.ToLong(r),

            BinOp.Lt => InterpreterValueOps.ToLong(l) < InterpreterValueOps.ToLong(r),
            BinOp.Le => InterpreterValueOps.ToLong(l) <= InterpreterValueOps.ToLong(r),
            BinOp.Gt => InterpreterValueOps.ToLong(l) > InterpreterValueOps.ToLong(r),
            BinOp.Ge => InterpreterValueOps.ToLong(l) >= InterpreterValueOps.ToLong(r),

            BinOp.Eq => Equals(
                objA: l,
                objB: r),
            BinOp.Ne => !Equals(
                objA: l,
                objB: r),

            BinOp.And or BinOp.Or => throw new RuntimeException("internal: &&/|| should be handled in Eval"),
            BinOp.Assign => throw new RuntimeException("internal: '=' handled in EvalAssign"),

            _ => throw new RuntimeException($"binary op '{op}' not implemented")
        };
    }

    private static object? ApplyUnary(
        UnOp op,
        object? r)
    {
        return op switch
        {
            UnOp.Neg => -InterpreterValueOps.ToLong(r),
            UnOp.Not => !InterpreterValueOps.IsTrue(r),
            UnOp.Plus => InterpreterValueOps.ToLong(r),
            _ => throw new RuntimeException($"unknown unary op '{op}'")
        };
    }

    private static object? ArrayGet(
        object? arrObj,
        int index)
    {
        if (arrObj is not Array arr)
        {
            throw new RuntimeException("indexing a non-array value");
        }

        if (index < 0 || index >= arr.Length)
        {
            throw new RuntimeException("array index out of bounds");
        }

        return arr.GetValue(index);
    }

    private static void ArraySet(
        object? arrObj,
        int index,
        object? value)
    {
        if (arrObj is not Array arr)
        {
            throw new RuntimeException("indexing a non-array value");
        }

        if (index < 0 || index >= arr.Length)
        {
            throw new RuntimeException("array index out of bounds");
        }

        arr.SetValue(
            value: value,
            index: index);
    }

    private object? Call(
        string name,
        object?[] args)
    {
        if (Builtins.TryInvoke(
                name: name,
                args: args,
                result: out object? builtin))
        {
            return builtin;
        }

        if (!_funcs.TryGetValue(
                key: name,
                value: out FuncHir? f))
        {
            throw new RuntimeException($"undefined function '{name}'");
        }

        if (f.Parameters.Count != args.Length)
        {
            throw new RuntimeException($"call to '{name}' expects {f.Parameters.Count} args, got {args.Length}");
        }

        var frame = new Frame();
        frame.PushScope();

        for (var i = 0; i < args.Length; i++)
        {
            frame.DefineLocal(
                name: f.Parameters[i],
                value: args[i]);
        }

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

    private Frame CurrentFrame()
    {
        return _stack.Peek();
    }

    private object? Eval(
        ExprHir? e)
    {
        return e switch
        {
            null => null,
            IntHir i => i.Value,
            CharHir c => c.Value,
            StringHir s => s.Value,
            BoolHir b => b.Value,
            VarHir v => Lookup(v.Name),
            UnHir u => ApplyUnary(
                op: u.Op,
                r: Eval(u.Operand)),
            BinHir { Op: BinOp.Assign } a => EvalAssign(a),
            BinHir { Op: BinOp.And } b => InterpreterValueOps.IsTrue(Eval(b.Left)) && InterpreterValueOps.IsTrue(Eval(b.Right)),
            BinHir { Op: BinOp.Or } b2 => InterpreterValueOps.IsTrue(Eval(b2.Left)) || InterpreterValueOps.IsTrue(Eval(b2.Right)),
            BinHir b3 => ApplyBinary(
                op: b3.Op,
                l: Eval(b3.Left),
                r: Eval(b3.Right)),
            CallHir c => Call(
                name: (c.Callee as VarHir)?.Name
                ?? throw new RuntimeException("only simple function names are callable"),
                args: c
                    .Args
                    .Select(Eval)
                    .ToArray()),
            IndexHir ix => ArrayGet(
                arrObj: Eval(ix.Target),
                index: (int)InterpreterValueOps.ToLong(Eval(ix.Index))),
            _ => throw new RuntimeException($"expr {e.GetType().Name} not implemented")
        };
    }

    private object? EvalAssign(
        BinHir a)
    {
        object? value = Eval(a.Right);

        switch (a.Left)
        {
            case VarHir v:
                SetVar(
                    name: v.Name,
                    val: value);

                return value;
            case IndexHir ix:
                ArraySet(
                    arrObj: Eval(ix.Target),
                    index: (int)InterpreterValueOps.ToLong(Eval(ix.Index)),
                    value: value);

                return value;
            default:
                throw new RuntimeException("invalid assignment target");
        }
    }

    private void ExecBlock(
        BlockHir b)
    {
        Frame frame = CurrentFrame();
        frame.PushScope();

        try
        {
            foreach (StmtHir s in b.Statements)
            {
                ExecStmt(s);
            }
        }
        finally
        {
            frame.PopScope();
        }
    }

    private void ExecOptional(
        StmtHir? s)
    {
        if (s != null)
        {
            ExecStmt(s);
        }
    }

    private void ExecStmt(
        StmtHir s)
    {
        switch (s)
        {
            case VarDeclHir v:
                CurrentFrame()
                    .DefineLocal(
                        name: v.Name,
                        value: Eval(v.Init));

                break;
            case ExprStmtHir e:
                if (e.Expr != null)
                {
                    Eval(e.Expr);
                }

                break;
            case ReturnHir r:
                throw new ReturnSignal(Eval(r.Expr));
            case IfHir ifs:
                if (InterpreterValueOps.IsTrue(Eval(ifs.Cond)))
                {
                    ExecStmt(ifs.Then);
                }
                else if (ifs.Else != null)
                {
                    ExecStmt(ifs.Else);
                }

                break;
            case WhileHir w:
                while (InterpreterValueOps.IsTrue(Eval(w.Cond)))
                {
                    try { ExecStmt(w.Body); }
                    catch (ContinueSignal) { }
                    catch (BreakSignal) { break; }
                }

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

    private object? Lookup(
        string name)
    {
        return CurrentFrame()
            .Lookup(name);
    }

    private void SetVar(
        string name,
        object? val)
    {
        CurrentFrame()
            .SetVar(
                name: name,
                value: val);
    }

    private sealed class Frame
    {
        private readonly Stack<Dictionary<string, object?>> _scopes = new Stack<Dictionary<string, object?>>();

        public void DefineLocal(
            string name,
            object? value)
        {
            _scopes
                .Peek()[name] = value;
        }

        public object? Lookup(
            string name)
        {
            foreach (Dictionary<string, object?> scope in _scopes)
            {
                if (scope.TryGetValue(
                        key: name,
                        value: out object? value))
                {
                    return value;
                }
            }

            throw new RuntimeException($"unbound variable '{name}'");
        }

        public void PopScope()
        {
            _scopes.Pop();
        }

        public void PushScope()
        {
            _scopes.Push(new Dictionary<string, object?>());
        }

        public void SetVar(
            string name,
            object? value)
        {
            foreach (Dictionary<string, object?> scope in _scopes)
            {
                if (scope.ContainsKey(name))
                {
                    scope[name] = value;

                    return;
                }
            }

            throw new RuntimeException($"unbound variable '{name}'");
        }
    }
}
