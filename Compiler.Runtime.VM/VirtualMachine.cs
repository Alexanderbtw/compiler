using Compiler.Core.Builtins;
using Compiler.Core.Operations;
using Compiler.Runtime.VM.Bytecode;
using Compiler.Runtime.VM.Execution;
using Compiler.Runtime.VM.Execution.GC;
using Compiler.Runtime.VM.Options;

namespace Compiler.Runtime.VM;

/// <summary>
///     Register-based virtual machine with a handle-addressed heap and mark-sweep GC.
/// </summary>
public sealed class VirtualMachine : IVmExecutionRuntime
{
    private readonly List<VmValue[]> _constantRoots = [];
    private readonly List<CompiledFrameRoots> _compiledFrameRoots = [];
    private readonly List<CallFrame> _frames = [];
    private readonly GcHeap _gcHeap;
    private readonly GcOptions _options;
    private VmValue[] _stack = new VmValue[256];
    private int _stackTop;

    public VirtualMachine(
        GcOptions? options = null)
    {
        _options = options ?? GcOptions.Default;
        _gcHeap = new GcHeap(
            initialThreshold: _options.InitialThreshold,
            growthFactor: _options.GrowthFactor);
    }

    public VmValue AllocateArray(
        int length)
    {
        TryCollect();

        return VmValue.FromHandle(_gcHeap.AllocateArray(length));
    }

    public VmValue AllocateString(
        string value)
    {
        TryCollect();

        return VmValue.FromHandle(_gcHeap.AllocateString(value));
    }

    public VmValue Execute(
        VmProgram program,
        string entryFunctionName)
    {
        return Execute(
            program: program,
            entryFunctionName: entryFunctionName,
            observer: null);
    }

    /// <summary>
    ///     Executes a bytecode program with optional execution observation and tier switching.
    /// </summary>
    /// <param name="program">Program to execute.</param>
    /// <param name="entryFunctionName">Entry function name.</param>
    /// <param name="observer">Optional execution observer.</param>
    /// <returns>Program result.</returns>
    public VmValue Execute(
        VmProgram program,
        string entryFunctionName,
        IVmExecutionObserver? observer)
    {
        ArgumentNullException.ThrowIfNull(program);
        ArgumentException.ThrowIfNullOrWhiteSpace(entryFunctionName);

        if (!program.Functions.TryGetValue(
                key: entryFunctionName,
                value: out VmFunction? entryFunction))
        {
            throw new InvalidOperationException($"entry '{entryFunctionName}' not found");
        }

        ResetExecutionState();

        try
        {
            if (observer is not null &&
                observer.TryInvokeFunction(
                    vm: this,
                    function: entryFunction,
                    arguments: [],
                    result: out VmValue compiledEntryResult))
            {
                return compiledEntryResult;
            }

            Dictionary<VmFunction, VmValue[]> constantCache = MaterializeConstants(program);
            PushFrame(
                function: entryFunction,
                arguments: [],
                returnSlot: -1,
                constants: constantCache[entryFunction],
                observer: observer);

            while (_frames.Count > 0)
            {
                VmValue? finished = Step(
                    functions: program.Functions,
                    constantCache: constantCache,
                    observer: observer);

                if (finished is { } result)
                {
                    return result;
                }
            }

            return VmValue.Null;
        }
        finally
        {
            ResetExecutionState();
        }
    }

    public object? ExportValue(
        VmValue value)
    {
        return ExportValue(
            value: value,
            seenHandles: []);
    }

    /// <inheritdoc />
    public string FormatValue(
        VmValue value)
    {
        return value.Kind switch
        {
            VmValueKind.Null => "null",
            VmValueKind.I64 => value
                .AsInt64()
                .ToString(),
            VmValueKind.Bool => value.AsBool()
                ? "true"
                : "false",
            VmValueKind.Char => value
                .AsChar()
                .ToString(),
            VmValueKind.Ref => GetHeapObjectKind(value.AsHandle()) switch
            {
                HeapObjectKind.String => GetString(value.AsHandle()),
                HeapObjectKind.Array => "[array]",
                _ => throw new ArgumentOutOfRangeException()
            },
            _ => throw new ArgumentOutOfRangeException()
        };
    }

    /// <inheritdoc />
    public VmValue GetArrayElement(
        int handle,
        int index)
    {
        ArrayObject arrayObject = _gcHeap.GetRequiredArray(handle);

        if (index < 0 || index >= arrayObject.Elements.Length)
        {
            throw new InvalidOperationException("array index out of bounds");
        }

        return arrayObject.Elements[index];
    }

    /// <inheritdoc />
    public int GetArrayLength(
        int handle)
    {
        return _gcHeap.GetRequiredArray(handle)
            .Elements.Length;
    }

    public GcStats GetGcStats()
    {
        return _gcHeap.GetStats();
    }

    /// <inheritdoc />
    public HeapObjectKind GetHeapObjectKind(
        int handle)
    {
        return _gcHeap.GetHeapObjectKind(handle);
    }

    /// <inheritdoc />
    public string GetString(
        int handle)
    {
        return _gcHeap.GetRequiredString(handle)
            .Text;
    }

    public bool IsAliveHandle(
        int handle)
    {
        return _gcHeap.IsAliveHandle(handle);
    }

    /// <inheritdoc />
    public void SetArrayElement(
        int handle,
        int index,
        VmValue value)
    {
        ArrayObject arrayObject = _gcHeap.GetRequiredArray(handle);

        if (index < 0 || index >= arrayObject.Elements.Length)
        {
            throw new InvalidOperationException("array index out of bounds");
        }

        arrayObject.Elements[index] = value;
    }

    /// <inheritdoc />
    public void EnterCompiledFrame(
        VmValue[] locals,
        VmValue[] constants)
    {
        ArgumentNullException.ThrowIfNull(locals);
        ArgumentNullException.ThrowIfNull(constants);

        _compiledFrameRoots.Add(new CompiledFrameRoots(
            locals: locals,
            constants: constants));
    }

    /// <inheritdoc />
    public void ExitCompiledFrame()
    {
        if (_compiledFrameRoots.Count == 0)
        {
            throw new InvalidOperationException("no compiled frame is active");
        }

        _compiledFrameRoots.RemoveAt(_compiledFrameRoots.Count - 1);
    }

    private static VmValue MaterializeConstant(
        VmConstant constant,
        VirtualMachine vm)
    {
        return constant.Kind switch
        {
            VmConstantKind.Null => VmValue.Null,
            VmConstantKind.I64 => VmValue.FromLong(constant.Payload),
            VmConstantKind.Bool => VmValue.FromBool(constant.Payload != 0),
            VmConstantKind.Char => VmValue.FromChar((char)constant.Payload),
            VmConstantKind.String => vm.AllocateString(constant.Text ?? string.Empty),
            _ => throw new ArgumentOutOfRangeException()
        };
    }

    private void EnsureStackCapacity(
        int required)
    {
        if (required <= _stack.Length)
        {
            return;
        }

        int nextSize = _stack.Length;

        while (nextSize < required)
        {
            nextSize *= 2;
        }

        Array.Resize(
            array: ref _stack,
            newSize: nextSize);
    }

    private IEnumerable<VmValue> EnumerateRoots()
    {
        for (var index = 0; index < _stackTop; index++)
        {
            yield return _stack[index];
        }

        foreach (VmValue[] constants in _constantRoots)
        {
            foreach (VmValue value in constants)
            {
                yield return value;
            }
        }

        foreach (CompiledFrameRoots frameRoots in _compiledFrameRoots)
        {
            foreach (VmValue value in frameRoots.Locals)
            {
                yield return value;
            }

            foreach (VmValue value in frameRoots.Constants)
            {
                yield return value;
            }
        }
    }

    private VmValue ExecuteBinary(
        VmBinaryInstruction instruction,
        CallFrame frame)
    {
        VmValue left = ReadOperand(
            frame: frame,
            operand: instruction.Left);

        VmValue right = ReadOperand(
            frame: frame,
            operand: instruction.Right);

        return instruction.Operation switch
        {
            MBinOp.Add => VmValue.FromLong(left.AsInt64() + right.AsInt64()),
            MBinOp.Sub => VmValue.FromLong(left.AsInt64() - right.AsInt64()),
            MBinOp.Mul => VmValue.FromLong(left.AsInt64() * right.AsInt64()),
            MBinOp.Div => VmValue.FromLong(left.AsInt64() / right.AsInt64()),
            MBinOp.Mod => VmValue.FromLong(left.AsInt64() % right.AsInt64()),
            MBinOp.Eq => VmValue.FromBool(
                VmValueOps.AreEqual(
                    left: left,
                    right: right,
                    vm: this)),
            MBinOp.Ne => VmValue.FromBool(
                !VmValueOps.AreEqual(
                    left: left,
                    right: right,
                    vm: this)),
            MBinOp.Lt => VmValue.FromBool(left.AsInt64() < right.AsInt64()),
            MBinOp.Le => VmValue.FromBool(left.AsInt64() <= right.AsInt64()),
            MBinOp.Gt => VmValue.FromBool(left.AsInt64() > right.AsInt64()),
            MBinOp.Ge => VmValue.FromBool(left.AsInt64() >= right.AsInt64()),
            _ => throw new NotSupportedException(instruction.Operation.ToString())
        };
    }

    private VmValue ExecuteUnary(
        VmUnaryInstruction instruction,
        CallFrame frame)
    {
        VmValue operand = ReadOperand(
            frame: frame,
            operand: instruction.Operand);

        return instruction.Operation switch
        {
            MUnOp.Neg => VmValue.FromLong(-operand.AsInt64()),
            MUnOp.Not => VmValue.FromBool(
                !VmValueOps.ToBool(
                    value: operand,
                    vm: this)),
            MUnOp.Plus => VmValue.FromLong(operand.AsInt64()),
            _ => throw new NotSupportedException(instruction.Operation.ToString())
        };
    }

    private object? ExportArray(
        int handle,
        HashSet<int> seenHandles)
    {
        if (!seenHandles.Add(handle))
        {
            return "[cyclic]";
        }

        ArrayObject arrayObject = _gcHeap.GetRequiredArray(handle);
        var result = new object?[arrayObject.Elements.Length];

        for (var index = 0; index < arrayObject.Elements.Length; index++)
        {
            result[index] = ExportValue(
                value: arrayObject.Elements[index],
                seenHandles: seenHandles);
        }

        seenHandles.Remove(handle);

        return result;
    }

    private object? ExportValue(
        VmValue value,
        HashSet<int> seenHandles)
    {
        return value.Kind switch
        {
            VmValueKind.Null => null,
            VmValueKind.I64 => value.AsInt64(),
            VmValueKind.Bool => value.AsBool(),
            VmValueKind.Char => value.AsChar(),
            VmValueKind.Ref => GetHeapObjectKind(value.AsHandle()) switch
            {
                HeapObjectKind.String => GetString(value.AsHandle()),
                HeapObjectKind.Array => ExportArray(
                    handle: value.AsHandle(),
                    seenHandles: seenHandles),
                _ => throw new ArgumentOutOfRangeException()
            },
            _ => throw new ArgumentOutOfRangeException()
        };
    }

    private Dictionary<VmFunction, VmValue[]> MaterializeConstants(
        VmProgram program)
    {
        var cache = new Dictionary<VmFunction, VmValue[]>();

        foreach (VmFunction function in program.Functions.Values)
        {
            var values = new VmValue[function.Constants.Count];
            _constantRoots.Add(values);

            for (var index = 0; index < function.Constants.Count; index++)
            {
                values[index] = MaterializeConstant(
                    constant: function.Constants[index],
                    vm: this);
            }

            cache[function] = values;
        }

        return cache;
    }

    private void PushFrame(
        VmFunction function,
        ReadOnlySpan<VmValue> arguments,
        int returnSlot,
        VmValue[] constants,
        IVmExecutionObserver? observer)
    {
        if (arguments.Length != function.ParameterCount)
        {
            throw new InvalidOperationException($"call to '{function.Name}' expects {function.ParameterCount} args, got {arguments.Length}");
        }

        int baseSlot = _stackTop;
        EnsureStackCapacity(baseSlot + function.RegisterCount);
        Array.Fill(
            array: _stack,
            value: VmValue.Null,
            startIndex: baseSlot,
            count: function.RegisterCount);

        for (var index = 0; index < arguments.Length; index++)
        {
            _stack[baseSlot + function.ParameterRegisters[index]] = arguments[index];
        }

        _stackTop += function.RegisterCount;
        _frames.Add(
            new CallFrame(
                function: function,
                constants: constants,
                baseSlot: baseSlot,
                returnSlot: returnSlot));
        observer?.OnFunctionEntered(function);
    }

    private VmValue ReadOperand(
        CallFrame frame,
        VmOperand operand)
    {
        return operand.Kind switch
        {
            VmOperandKind.Register => _stack[frame.BaseSlot + operand.Index],
            VmOperandKind.Constant => frame.Constants[operand.Index],
            _ => throw new ArgumentOutOfRangeException()
        };
    }

    private void ResetExecutionState()
    {
        _frames.Clear();
        _constantRoots.Clear();
        _compiledFrameRoots.Clear();
        _stackTop = 0;
    }

    private VmValue? Step(
        IReadOnlyDictionary<string, VmFunction> functions,
        IReadOnlyDictionary<VmFunction, VmValue[]> constantCache,
        IVmExecutionObserver? observer)
    {
        CallFrame frame = _frames[^1];

        if (frame.InstructionPointer < 0 || frame.InstructionPointer >= frame.Function.Instructions.Count)
        {
            throw new InvalidOperationException($"instruction pointer out of range in '{frame.Function.Name}'");
        }

        VmInstruction instruction = frame.Function.Instructions[frame.InstructionPointer];
        frame.InstructionPointer++;
        _frames[^1] = frame;

        switch (instruction)
        {
            case VmMoveInstruction move:
                _stack[frame.BaseSlot + move.DestinationRegister] = ReadOperand(
                    frame: frame,
                    operand: move.Source);

                return null;

            case VmBinaryInstruction binary:
                _stack[frame.BaseSlot + binary.DestinationRegister] = ExecuteBinary(
                    instruction: binary,
                    frame: frame);

                return null;

            case VmUnaryInstruction unary:
                _stack[frame.BaseSlot + unary.DestinationRegister] = ExecuteUnary(
                    instruction: unary,
                    frame: frame);

                return null;

            case VmLoadIndexInstruction loadIndex:
                {
                    VmValue arrayValue = ReadOperand(
                        frame: frame,
                        operand: loadIndex.ArrayOperand);

                    VmValue indexValue = ReadOperand(
                        frame: frame,
                        operand: loadIndex.IndexOperand);

                    _stack[frame.BaseSlot + loadIndex.DestinationRegister] = GetArrayElement(
                        handle: arrayValue.AsHandle(),
                        index: checked((int)indexValue.AsInt64()));

                    return null;
                }

            case VmStoreIndexInstruction storeIndex:
                {
                    VmValue arrayValue = ReadOperand(
                        frame: frame,
                        operand: storeIndex.ArrayOperand);

                    VmValue indexValue = ReadOperand(
                        frame: frame,
                        operand: storeIndex.IndexOperand);

                    VmValue value = ReadOperand(
                        frame: frame,
                        operand: storeIndex.ValueOperand);

                    SetArrayElement(
                        handle: arrayValue.AsHandle(),
                        index: checked((int)indexValue.AsInt64()),
                        value: value);

                    return null;
                }

            case VmCallInstruction call:
                {
                    var arguments = new VmValue[call.Arguments.Count];

                    for (var index = 0; index < call.Arguments.Count; index++)
                    {
                        arguments[index] = ReadOperand(
                            frame: frame,
                            operand: call.Arguments[index]);
                    }

                    if (BuiltinCatalog.Exists(call.Callee))
                    {
                        VmValue result = VmBuiltins.Invoke(
                            name: call.Callee,
                            vm: this,
                            args: arguments);

                        if (call.DestinationRegister is { } dst)
                        {
                            _stack[frame.BaseSlot + dst] = result;
                        }

                        return null;
                    }

                    if (!functions.TryGetValue(
                            key: call.Callee,
                            value: out VmFunction? targetFunction))
                    {
                        throw new InvalidOperationException($"unknown function '{call.Callee}'");
                    }

                    if (observer is not null &&
                        observer.TryInvokeFunction(
                            vm: this,
                            function: targetFunction,
                            arguments: arguments,
                            result: out VmValue compiledResult))
                    {
                        if (call.DestinationRegister is { } compiledDestination)
                        {
                            _stack[frame.BaseSlot + compiledDestination] = compiledResult;
                        }

                        return null;
                    }

                    PushFrame(
                        function: targetFunction,
                        arguments: arguments,
                        returnSlot: call.DestinationRegister is { } callDst
                            ? frame.BaseSlot + callDst
                            : -1,
                        constants: constantCache[targetFunction],
                        observer: observer);

                    return null;
                }

            case VmBranchInstruction branch:
                if (branch.TargetInstruction <= frame.InstructionPointer - 1)
                {
                    observer?.OnLoopBackEdge(
                        function: frame.Function,
                        sourceInstruction: frame.InstructionPointer - 1,
                        targetInstruction: branch.TargetInstruction);
                }

                frame.InstructionPointer = branch.TargetInstruction;
                _frames[^1] = frame;

                return null;

            case VmBranchConditionInstruction branchCondition:
                int branchTarget = VmValueOps.ToBool(
                    value: ReadOperand(
                        frame: frame,
                        operand: branchCondition.Condition),
                    vm: this)
                    ? branchCondition.TrueTarget
                    : branchCondition.FalseTarget;

                if (branchTarget <= frame.InstructionPointer - 1)
                {
                    observer?.OnLoopBackEdge(
                        function: frame.Function,
                        sourceInstruction: frame.InstructionPointer - 1,
                        targetInstruction: branchTarget);
                }

                frame.InstructionPointer = branchTarget;

                _frames[^1] = frame;

                return null;

            case VmReturnInstruction ret:
                {
                    VmValue result = ret.Value is { } operand
                        ? ReadOperand(
                            frame: frame,
                            operand: operand)
                        : VmValue.Null;

                    int returnSlot = frame.ReturnSlot;
                    _stackTop = frame.BaseSlot;
                    _frames.RemoveAt(_frames.Count - 1);

                    if (_frames.Count == 0)
                    {
                        return result;
                    }

                    if (returnSlot >= 0)
                    {
                        _stack[returnSlot] = result;
                    }

                    return null;
                }

            default:
                throw new NotSupportedException(
                    instruction.GetType()
                        .Name);
        }
    }

    private void TryCollect()
    {
        if (_options.AutoCollect && _gcHeap.ShouldCollect())
        {
            _gcHeap.Collect(EnumerateRoots());
        }
    }

    private struct CallFrame(
        VmFunction function,
        VmValue[] constants,
        int baseSlot,
        int returnSlot)
    {
        public int BaseSlot { get; } = baseSlot;

        public VmValue[] Constants { get; } = constants;

        public VmFunction Function { get; } = function;

        public int InstructionPointer { get; set; }

        public int ReturnSlot { get; } = returnSlot;
    }

    private readonly struct CompiledFrameRoots(
        VmValue[] locals,
        VmValue[] constants)
    {
        public VmValue[] Constants { get; } = constants;

        public VmValue[] Locals { get; } = locals;
    }
}
