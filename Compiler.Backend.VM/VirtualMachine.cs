namespace Compiler.Backend.VM;

public sealed class VirtualMachine(
    VmModule module,
    int stackSize = 1024)
{
    private readonly Stack<CallFrame> _callStack = new Stack<CallFrame>(32);
    private readonly Value[] _operandStack = new Value[stackSize];

    private int _stackPointer;

    public Value Execute(
        string entryFunctionName = "main",
        ReadOnlySpan<Value> arguments = default)
    {
        if (!module.TryGetFunctionIndex(
                name: entryFunctionName,
                idx: out int entryIndex))
        {
            throw new InvalidOperationException($"entry '{entryFunctionName}' not found");
        }

        CallFrame currentFrame = CreateFrame(
            functionIndex: entryIndex,
            arguments: arguments);

        while (true)
        {
            if (currentFrame.ProgramCounter < 0 || currentFrame.ProgramCounter >= currentFrame.Function.Code.Count)
            {
                throw new InvalidOperationException("PC out of range");
            }

            Instr instruction = currentFrame.Function.Code[currentFrame.ProgramCounter++];

            switch (instruction.Op)
            {
                // constants / locals
                case OpCode.LdcNull:
                    PushValue(Value.Null);

                    break;

                case OpCode.LdcI64:
                    PushValue(Value.FromLong(instruction.Imm));

                    break;

                case OpCode.LdcBool:
                    PushValue(Value.FromBool(instruction.Imm != 0));

                    break;

                case OpCode.LdcChar:
                    PushValue(Value.FromChar((char)instruction.Imm));

                    break;

                case OpCode.LdcStr:
                    PushValue(Value.FromString(module.StringPool[instruction.Idx]));

                    break;

                case OpCode.LdLoc:
                    PushValue(currentFrame.Locals[instruction.A]);

                    break;

                case OpCode.StLoc:
                    currentFrame.Locals[instruction.A] = PopValue();

                    break;

                case OpCode.Pop:
                    PopValue();

                    break;

                // arithmetic / logic
                case OpCode.Add:
                    {
                        Value right = PopValue();
                        Value left = PopValue();
                        PushValue(Value.FromLong(left.AsInt64() + right.AsInt64()));

                        break;
                    }
                case OpCode.Sub:
                    {
                        Value right = PopValue();
                        Value left = PopValue();
                        PushValue(Value.FromLong(left.AsInt64() - right.AsInt64()));

                        break;
                    }
                case OpCode.Mul:
                    {
                        Value right = PopValue();
                        Value left = PopValue();
                        PushValue(Value.FromLong(left.AsInt64() * right.AsInt64()));

                        break;
                    }
                case OpCode.Div:
                    {
                        Value right = PopValue();
                        Value left = PopValue();
                        PushValue(Value.FromLong(left.AsInt64() / right.AsInt64()));

                        break;
                    }
                case OpCode.Mod:
                    {
                        Value right = PopValue();
                        Value left = PopValue();
                        PushValue(Value.FromLong(left.AsInt64() % right.AsInt64()));

                        break;
                    }
                case OpCode.Neg:
                    {
                        Value value = PopValue();
                        PushValue(Value.FromLong(-value.AsInt64()));

                        break;
                    }
                case OpCode.Not:
                    {
                        Value value = CoerceToBoolean(PopValue());
                        PushValue(Value.FromBool(!value.AsBool()));

                        break;
                    }

                case OpCode.Eq:
                    {
                        Value right = PopValue();
                        Value left = PopValue();
                        PushValue(
                            Value.FromBool(
                                AreValuesEqual(
                                    a: left,
                                    b: right)));

                        break;
                    }
                case OpCode.Ne:
                    {
                        Value right = PopValue();
                        Value left = PopValue();
                        PushValue(
                            Value.FromBool(
                                !AreValuesEqual(
                                    a: left,
                                    b: right)));

                        break;
                    }
                case OpCode.Lt:
                    {
                        Value right = PopValue();
                        Value left = PopValue();
                        PushValue(Value.FromBool(left.AsInt64() < right.AsInt64()));

                        break;
                    }
                case OpCode.Le:
                    {
                        Value right = PopValue();
                        Value left = PopValue();
                        PushValue(Value.FromBool(left.AsInt64() <= right.AsInt64()));

                        break;
                    }
                case OpCode.Gt:
                    {
                        Value right = PopValue();
                        Value left = PopValue();
                        PushValue(Value.FromBool(left.AsInt64() > right.AsInt64()));

                        break;
                    }
                case OpCode.Ge:
                    {
                        Value right = PopValue();
                        Value left = PopValue();
                        PushValue(Value.FromBool(left.AsInt64() >= right.AsInt64()));

                        break;
                    }

                // arrays
                case OpCode.LdElem:
                    {
                        int elementIndex = (int)PopValue()
                            .AsInt64();

                        VmArray array = PopValue()
                            .AsArray();

                        PushValue(array[elementIndex]);

                        break;
                    }
                case OpCode.StElem:
                    {
                        Value value = PopValue();
                        int elementIndex = (int)PopValue()
                            .AsInt64();

                        VmArray array = PopValue()
                            .AsArray();

                        array[elementIndex] = value;

                        break;
                    }

                // control flow
                case OpCode.Br:
                    currentFrame.ProgramCounter = instruction.A;

                    break;

                case OpCode.BrTrue:
                    if (CoerceToBoolean(PopValue())
                        .AsBool())
                    {
                        currentFrame.ProgramCounter = instruction.A;
                    }

                    break;

                case OpCode.Ret:
                    {
                        Value returnValue = PopValue();

                        if (_callStack.Count == 0)
                        {
                            return returnValue;
                        }

                        currentFrame = _callStack.Pop(); // restore caller
                        PushValue(returnValue);

                        break;
                    }

                // calls
                case OpCode.CallUser:
                    {
                        // Pop args (rightmost at stack top), pass in the order of function parameters
                        int argumentCount = instruction.B;
                        var callArguments = new Value[argumentCount];

                        for (int index = argumentCount - 1; index >= 0; --index)
                        {
                            callArguments[index] = PopValue();
                        }

                        _callStack.Push(currentFrame); // save caller
                        currentFrame = CreateFrame(
                            functionIndex: instruction.A,
                            arguments: callArguments);

                        break;
                    }

                case OpCode.CallBuiltin:
                    {
                        int argumentCount = instruction.B;
                        var callArguments = new Value[argumentCount];

                        for (int index = argumentCount - 1; index >= 0; --index)
                        {
                            callArguments[index] = PopValue();
                        }

                        string builtinName = module.StringPool[instruction.A];
                        Value result = BuiltinsVm.Invoke(
                            name: builtinName,
                            args: callArguments);

                        PushValue(result);

                        break;
                    }

                case OpCode.NewArr:
                    {
                        int length = (int)PopValue()
                            .AsInt64();

                        PushValue(Value.FromArray(new VmArray(length)));

                        break;
                    }

                case OpCode.Len:
                    {
                        Value value = PopValue();

                        switch (value.Tag)
                        {
                            case ValueTag.String:
                                PushValue(
                                    Value.FromLong(
                                        value.AsString()
                                            .Length));

                                break;
                            case ValueTag.Array:
                                PushValue(
                                    Value.FromLong(
                                        value.AsArray()
                                            .Length));

                                break;
                            default:
                                throw new InvalidOperationException("len: unsupported type");
                        }

                        break;
                    }

                default:
                    throw new NotSupportedException(instruction.Op.ToString());
            }
        }
    }

    private static bool AreValuesEqual(
        Value a,
        Value b)
    {
        if (a.Tag != b.Tag)
        {
            // attempt to compare i64 with char as numbers
            if (a.Tag == ValueTag.I64 && b.Tag == ValueTag.Char)
            {
                return a.AsInt64() == b.Char;
            }

            if (a.Tag == ValueTag.Char && b.Tag == ValueTag.I64)
            {
                return a.Char == b.AsInt64();
            }

            return false;
        }

        return a.Tag switch
        {
            ValueTag.Null => true,
            ValueTag.I64 => a.AsInt64() == b.AsInt64(),
            ValueTag.Bool => a.AsBool() == b.AsBool(),
            ValueTag.Char => a.Char == b.Char,
            ValueTag.String => a.AsString() == b.AsString(),
            ValueTag.Array => ReferenceEquals(
                objA: a.Ref,
                objB: b.Ref),
            _ => Equals(
                objA: a.Ref,
                objB: b.Ref)
        };
    }

    private static Value CoerceToBoolean(
        Value value)
    {
        return value.Tag switch
        {
            ValueTag.Bool => value,
            ValueTag.Null => Value.FromBool(false),
            ValueTag.I64 => Value.FromBool(value.AsInt64() != 0),
            ValueTag.Char => Value.FromBool(value.Char != '\0'),
            ValueTag.String => Value.FromBool(!string.IsNullOrEmpty(value.AsString())),
            ValueTag.Array => Value.FromBool(
                value.AsArray()
                    .Length != 0),
            _ => Value.FromBool(value.Ref is not null)
        };
    }

    private CallFrame CreateFrame(
        int functionIndex,
        ReadOnlySpan<Value> arguments)
    {
        VmFunction function = module.Functions[functionIndex];

        if (arguments.Length != function.Arity)
        {
            throw new InvalidOperationException($"{function.Name} expects {function.Arity} args, got {arguments.Length}");
        }

        var frame = new CallFrame
        {
            Function = function,
            Locals = new Value[function.NLocals],
            ProgramCounter = 0
        };

        // place parameters into their local slots
        for (int i = 0; i < function.Arity; i++)
        {
            frame.Locals[function.ParamLocalIndices[i]] = arguments[i];
        }

        return frame;
    }

    private Value PopValue()
    {
        return _operandStack[--_stackPointer];
    }

    private void PushValue(
        Value value)
    {
        _operandStack[_stackPointer++] = value;
    }

    private struct CallFrame
    {
        public VmFunction Function;
        public Value[] Locals;
        public int ProgramCounter;
    }
}
