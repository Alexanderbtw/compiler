namespace Compiler.Backend.VM;

public sealed class VirtualMachine(
    VmModule mod,
    int stackSize = 1024)
{
    private readonly Stack<Frame> _call = new Stack<Frame>(32);

    private int _sp;
    private readonly Value[] _stack = new Value[stackSize];

    public Value Execute(
        string entry = "main",
        ReadOnlySpan<Value> args = default)
    {
        if (!mod.TryGetFunctionIndex(
                name: entry,
                idx: out int i))
        {
            throw new InvalidOperationException($"entry '{entry}' not found");
        }

        Frame cur = NewFrame(
            fnIdx: i,
            args: args);

        while (true)
        {
            if (cur.Pc < 0 || cur.Pc >= cur.Fn.Code.Count)
            {
                throw new InvalidOperationException("PC out of range");
            }

            Instr ins = cur.Fn.Code[cur.Pc++];

            switch (ins.Op)
            {
                // consts / locals
                case OpCode.LdcNull:
                    Push(Value.Null);

                    break;
                case OpCode.LdcI64:
                    Push(Value.FromLong(ins.Imm));

                    break;
                case OpCode.LdcBool:
                    Push(Value.FromBool(ins.Imm != 0));

                    break;
                case OpCode.LdcChar:
                    Push(Value.FromChar((char)ins.Imm));

                    break;
                case OpCode.LdcStr:
                    Push(Value.FromString(mod.StringPool[ins.Idx]));

                    break;

                case OpCode.LdLoc:
                    Push(cur.Locals[ins.A]);

                    break;
                case OpCode.StLoc:
                    cur.Locals[ins.A] = Pop();

                    break;
                case OpCode.Pop:
                    Pop();

                    break;

                // arithmetic / logic
                case OpCode.Add:
                    {
                        Value r = Pop();
                        Value l = Pop();
                        Push(Value.FromLong(l.AsLong() + r.AsLong()));

                        break;
                    }
                case OpCode.Sub:
                    {
                        Value r = Pop();
                        Value l = Pop();
                        Push(Value.FromLong(l.AsLong() - r.AsLong()));

                        break;
                    }
                case OpCode.Mul:
                    {
                        Value r = Pop();
                        Value l = Pop();
                        Push(Value.FromLong(l.AsLong() * r.AsLong()));

                        break;
                    }
                case OpCode.Div:
                    {
                        Value r = Pop();
                        Value l = Pop();
                        Push(Value.FromLong(l.AsLong() / r.AsLong()));

                        break;
                    }
                case OpCode.Mod:
                    {
                        Value r = Pop();
                        Value l = Pop();
                        Push(Value.FromLong(l.AsLong() % r.AsLong()));

                        break;
                    }
                case OpCode.Neg:
                    {
                        Value x = Pop();
                        Push(Value.FromLong(-x.AsLong()));

                        break;
                    }
                case OpCode.Not:
                    {
                        Value x = CoerceBool(Pop());
                        Push(Value.FromBool(!x.AsBool()));

                        break;
                    }

                case OpCode.Eq:
                    {
                        Value r = Pop();
                        Value l = Pop();
                        Push(
                            Value.FromBool(
                                EqualsVal(
                                    a: l,
                                    b: r)));

                        break;
                    }
                case OpCode.Ne:
                    {
                        Value r = Pop();
                        Value l = Pop();
                        Push(
                            Value.FromBool(
                                !EqualsVal(
                                    a: l,
                                    b: r)));

                        break;
                    }
                case OpCode.Lt:
                    {
                        Value r = Pop();
                        Value l = Pop();
                        Push(Value.FromBool(l.AsLong() < r.AsLong()));

                        break;
                    }
                case OpCode.Le:
                    {
                        Value r = Pop();
                        Value l = Pop();
                        Push(Value.FromBool(l.AsLong() <= r.AsLong()));

                        break;
                    }
                case OpCode.Gt:
                    {
                        Value r = Pop();
                        Value l = Pop();
                        Push(Value.FromBool(l.AsLong() > r.AsLong()));

                        break;
                    }
                case OpCode.Ge:
                    {
                        Value r = Pop();
                        Value l = Pop();
                        Push(Value.FromBool(l.AsLong() >= r.AsLong()));

                        break;
                    }

                // arrays
                case OpCode.LdElem:
                    {
                        int idx = (int)Pop()
                            .AsLong();

                        VmArray arr = Pop()
                            .AsArr();

                        Push(arr[idx]);

                        break;
                    }
                case OpCode.StElem:
                    {
                        Value val = Pop();
                        int idx = (int)Pop()
                            .AsLong();

                        VmArray arr = Pop()
                            .AsArr();

                        arr[idx] = val;

                        break;
                    }

                // control
                case OpCode.Br:
                    cur.Pc = ins.A;

                    break;
                case OpCode.BrTrue:
                    if (CoerceBool(Pop())
                        .AsBool())
                    {
                        cur.Pc = ins.A;
                    }

                    break;
                case OpCode.Ret:
                    {
                        Value ret = Pop();

                        if (_call.Count == 0)
                        {
                            return ret;
                        }

                        cur = _call.Pop(); // restore caller
                        Push(ret);

                        break;
                    }

                // calls
                case OpCode.CallUser:
                    {
                        // pop args (rightmost at stack top), pass in order of function parameters
                        int argc = ins.B;
                        var argv = new Value[argc];

                        for (int idx = argc - 1; idx >= 0; --idx)
                        {
                            argv[idx] = Pop();
                        }

                        _call.Push(cur); // save caller
                        cur = NewFrame(
                            fnIdx: ins.A,
                            args: argv);

                        break;
                    }
                case OpCode.CallBuiltin:
                    {
                        int argc = ins.B;
                        var argv = new Value[argc];

                        for (int idx = argc - 1; idx >= 0; --idx)
                        {
                            argv[idx] = Pop();
                        }

                        string name = mod.StringPool[ins.A];
                        Value r = BuiltinsVm.Invoke(
                            name: name,
                            args: argv);

                        Push(r);

                        break;
                    }

                default:
                    throw new NotSupportedException(ins.Op.ToString());
            }
        }
    }

    private static Value CoerceBool(
        Value v)
    {
        return v.Tag switch
        {
            ValueTag.Bool => v,
            ValueTag.Null => Value.FromBool(false),
            ValueTag.I64 => Value.FromBool(v.I64 != 0),
            ValueTag.Char => Value.FromBool(v.C != '\0'),
            ValueTag.String => Value.FromBool(!string.IsNullOrEmpty(v.AsStr())),
            ValueTag.Array => Value.FromBool(
                v.AsArr()
                    .Length != 0),
            _ => Value.FromBool(v.Ref is not null)
        };
    }

    private static bool EqualsVal(
        Value a,
        Value b)
    {
        if (a.Tag != b.Tag)
        {
            // попытка сравнить i64 с char как числа
            if (a.Tag == ValueTag.I64 && b.Tag == ValueTag.Char)
            {
                return a.I64 == b.C;
            }

            if (a.Tag == ValueTag.Char && b.Tag == ValueTag.I64)
            {
                return a.C == b.I64;
            }

            return false;
        }

        return a.Tag switch
        {
            ValueTag.Null => true,
            ValueTag.I64 => a.I64 == b.I64,
            ValueTag.Bool => a.B == b.B,
            ValueTag.Char => a.C == b.C,
            ValueTag.String => a.AsStr() == b.AsStr(),
            ValueTag.Array => ReferenceEquals(
                objA: a.Ref,
                objB: b.Ref),
            _ => Equals(
                objA: a.Ref,
                objB: b.Ref)
        };
    }

    private Frame NewFrame(
        int fnIdx,
        ReadOnlySpan<Value> args)
    {
        VmFunction fn = mod.Functions[fnIdx];

        if (args.Length != fn.Arity)
        {
            throw new InvalidOperationException($"{fn.Name} expects {fn.Arity} args, got {args.Length}");
        }

        var frame = new Frame { Fn = fn, Locals = new Value[fn.NLocals], Pc = 0 };

        // разместить параметры в их локальных слотах
        for (int i = 0; i < fn.Arity; i++)
        {
            frame.Locals[fn.ParamLocalIndices[i]] = args[i];
        }

        return frame;
    }
    private Value Pop()
    {
        return _stack[--_sp];
    }

    private void Push(
        Value v)
    {
        _stack[_sp++] = v;
    }

    private struct Frame
    {
        public VmFunction Fn;
        public Value[] Locals;
        public int Pc;
    }
}
