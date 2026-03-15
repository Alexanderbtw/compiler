using System.Linq.Expressions;
using System.Reflection;

using Compiler.Core.Builtins;
using Compiler.Core.Operations;
using Compiler.Runtime.VM;
using Compiler.Runtime.VM.Bytecode;
using Compiler.Runtime.VM.Execution;

namespace Compiler.Backend.CLR;

/// <summary>
///     Compiles VM bytecode into CLR delegates while preserving the current VM runtime/value model.
/// </summary>
public sealed class VmClrJitCompiler
{
    private static readonly FieldInfo FiVmNull = typeof(VmValue).GetField(nameof(VmValue.Null))!;
    private static readonly MethodInfo MiAllocateString = typeof(IVmExecutionRuntime).GetMethod(nameof(IVmExecutionRuntime.AllocateString))!;
    private static readonly MethodInfo MiAreEqual = typeof(VmValueOps).GetMethod(nameof(VmValueOps.AreEqual))!;
    private static readonly MethodInfo MiBuiltinInvoke = typeof(VmBuiltins).GetMethod(
        nameof(VmBuiltins.Invoke),
        [typeof(string), typeof(IVmExecutionRuntime), typeof(VmValue[])])!;
    private static readonly MethodInfo MiContextEnter = typeof(VmClrExecutionContext).GetMethod(nameof(VmClrExecutionContext.EnterFrame))!;
    private static readonly MethodInfo MiContextExit = typeof(VmClrExecutionContext).GetMethod(nameof(VmClrExecutionContext.ExitFrame))!;
    private static readonly MethodInfo MiContextInvoke = typeof(VmClrExecutionContext).GetMethod(nameof(VmClrExecutionContext.InvokeFunction))!;
    private static readonly MethodInfo MiFromBool = typeof(VmValue).GetMethod(nameof(VmValue.FromBool))!;
    private static readonly MethodInfo MiFromChar = typeof(VmValue).GetMethod(nameof(VmValue.FromChar))!;
    private static readonly MethodInfo MiFromLong = typeof(VmValue).GetMethod(nameof(VmValue.FromLong))!;
    private static readonly MethodInfo MiI64 = typeof(VmClrJitHelpers).GetMethod(nameof(VmClrJitHelpers.I64))!;
    private static readonly MethodInfo MiLoadIndex = typeof(VmClrJitHelpers).GetMethod(nameof(VmClrJitHelpers.LoadIndex))!;
    private static readonly MethodInfo MiRuntime = typeof(VmClrExecutionContext).GetProperty(nameof(VmClrExecutionContext.Runtime))!.GetMethod!;
    private static readonly MethodInfo MiStoreIndex = typeof(VmClrJitHelpers).GetMethod(nameof(VmClrJitHelpers.StoreIndex))!;
    private static readonly MethodInfo MiToBool = typeof(VmValueOps).GetMethod(nameof(VmValueOps.ToBool))!;
    private static readonly MethodInfo MiValidateArity = typeof(VmClrJitHelpers).GetMethod(nameof(VmClrJitHelpers.ValidateArity))!;

    /// <summary>
    ///     Compiles a bytecode module into CLR delegates.
    /// </summary>
    /// <param name="program">Bytecode module.</param>
    /// <returns>A CLR-compiled program.</returns>
    public VmClrCompiledProgram Compile(
        VmProgram program)
    {
        ArgumentNullException.ThrowIfNull(program);

        var functions = new Dictionary<string, VmClrJitFunc>(
            capacity: program.Functions.Count,
            comparer: StringComparer.Ordinal);

        foreach ((string functionName, VmFunction function) in program.Functions)
        {
            functions[functionName] = CompileFunction(function);
        }

        return new VmClrCompiledProgram(functions);
    }

    private static VmClrJitFunc CompileFunction(
        VmFunction function)
    {
        ParameterExpression context = Expression.Parameter(
            typeof(VmClrExecutionContext),
            "context");
        ParameterExpression args = Expression.Parameter(
            typeof(VmValue[]),
            "args");
        ParameterExpression locals = Expression.Variable(
            typeof(VmValue[]),
            "locals");
        ParameterExpression constants = Expression.Variable(
            typeof(VmValue[]),
            "constants");
        ParameterExpression result = Expression.Variable(
            typeof(VmValue),
            "result");
        LabelTarget returnTarget = Expression.Label(
            typeof(VmValue),
            $"{function.Name}_ret");
        LabelTarget[] instructionLabels = function.Instructions
            .Select((_, index) => Expression.Label($"i{index}"))
            .ToArray();
        var expressions = new List<Expression>
        {
            Expression.Assign(
                left: locals,
                right: Expression.NewArrayBounds(
                    typeof(VmValue),
                    Expression.Constant(Math.Max(0, function.RegisterCount)))),
            Expression.Assign(
                left: constants,
                right: Expression.NewArrayBounds(
                    typeof(VmValue),
                    Expression.Constant(Math.Max(0, function.Constants.Count)))),
            Expression.Call(
                instance: context,
                method: MiContextEnter,
                arguments: [locals, constants]),
            Expression.Call(
                method: MiValidateArity,
                arguments:
                [
                    Expression.Constant(function.Name),
                    Expression.Constant(function.ParameterCount),
                    Expression.ArrayLength(args)
                ])
        };

        EmitConstantInitialization(
            expressions: expressions,
            function: function,
            context: context,
            constants: constants);
        EmitParameterBinding(
            expressions: expressions,
            function: function,
            args: args,
            locals: locals);

        if (instructionLabels.Length > 0)
        {
            expressions.Add(Expression.Goto(instructionLabels[0]));
        }

        for (var instructionIndex = 0; instructionIndex < function.Instructions.Count; instructionIndex++)
        {
            expressions.Add(Expression.Label(instructionLabels[instructionIndex]));
            expressions.Add(
                BuildInstruction(
                    function: function,
                    instruction: function.Instructions[instructionIndex],
                    instructionLabels: instructionLabels,
                    context: context,
                    locals: locals,
                    constants: constants,
                    result: result,
                    returnTarget: returnTarget));
        }

        expressions.Add(
            Expression.Assign(
                left: result,
                right: Expression.Field(
                    expression: null,
                    field: FiVmNull)));
        expressions.Add(
            Expression.Call(
                instance: context,
                method: MiContextExit));
        expressions.Add(
            Expression.Label(
                target: returnTarget,
                defaultValue: result));

        Expression<VmClrJitFunc> lambda = Expression.Lambda<VmClrJitFunc>(
            body: Expression.Block(
                variables: [locals, constants, result],
                expressions),
            parameters: [context, args]);

        return lambda.Compile();
    }

    private static Expression BuildBinaryInstruction(
        VmBinaryInstruction instruction,
        Expression context,
        Expression locals,
        Expression constants)
    {
        Expression left = BuildOperandRead(
            locals: locals,
            constants: constants,
            operand: instruction.Left);
        Expression right = BuildOperandRead(
            locals: locals,
            constants: constants,
            operand: instruction.Right);
        Expression runtime = Expression.Call(
            instance: context,
            method: MiRuntime);

        return instruction.Operation switch
        {
            MBinOp.Add => Expression.Call(
                method: MiFromLong,
                arguments:
                [
                    Expression.Add(
                        left: Expression.Call(MiI64, left),
                        right: Expression.Call(MiI64, right))
                ]),
            MBinOp.Sub => Expression.Call(
                method: MiFromLong,
                arguments:
                [
                    Expression.Subtract(
                        left: Expression.Call(MiI64, left),
                        right: Expression.Call(MiI64, right))
                ]),
            MBinOp.Mul => Expression.Call(
                method: MiFromLong,
                arguments:
                [
                    Expression.Multiply(
                        left: Expression.Call(MiI64, left),
                        right: Expression.Call(MiI64, right))
                ]),
            MBinOp.Div => Expression.Call(
                method: MiFromLong,
                arguments:
                [
                    Expression.Divide(
                        left: Expression.Call(MiI64, left),
                        right: Expression.Call(MiI64, right))
                ]),
            MBinOp.Mod => Expression.Call(
                method: MiFromLong,
                arguments:
                [
                    Expression.Modulo(
                        left: Expression.Call(MiI64, left),
                        right: Expression.Call(MiI64, right))
                ]),
            MBinOp.Lt => Expression.Call(
                method: MiFromBool,
                arguments:
                [
                    Expression.LessThan(
                        left: Expression.Call(MiI64, left),
                        right: Expression.Call(MiI64, right))
                ]),
            MBinOp.Le => Expression.Call(
                method: MiFromBool,
                arguments:
                [
                    Expression.LessThanOrEqual(
                        left: Expression.Call(MiI64, left),
                        right: Expression.Call(MiI64, right))
                ]),
            MBinOp.Gt => Expression.Call(
                method: MiFromBool,
                arguments:
                [
                    Expression.GreaterThan(
                        left: Expression.Call(MiI64, left),
                        right: Expression.Call(MiI64, right))
                ]),
            MBinOp.Ge => Expression.Call(
                method: MiFromBool,
                arguments:
                [
                    Expression.GreaterThanOrEqual(
                        left: Expression.Call(MiI64, left),
                        right: Expression.Call(MiI64, right))
                ]),
            MBinOp.Eq => Expression.Call(
                method: MiFromBool,
                arguments:
                [
                    Expression.Call(
                        method: MiAreEqual,
                        arguments: [left, right, runtime])
                ]),
            MBinOp.Ne => Expression.Call(
                method: MiFromBool,
                arguments:
                [
                    Expression.Not(
                        Expression.Call(
                            method: MiAreEqual,
                            arguments: [left, right, runtime]))
                ]),
            _ => throw new ArgumentOutOfRangeException(nameof(instruction))
        };
    }

    private static Expression BuildCallInstruction(
        VmCallInstruction instruction,
        Expression context,
        Expression locals,
        Expression constants)
    {
        Expression arguments = Expression.NewArrayInit(
            typeof(VmValue),
            instruction.Arguments.Select(operand => BuildOperandRead(
                locals: locals,
                constants: constants,
                operand: operand)));
        Expression runtime = Expression.Call(
            instance: context,
            method: MiRuntime);

        return BuiltinCatalog.Exists(instruction.Callee)
            ? Expression.Call(
                method: MiBuiltinInvoke,
                arguments:
                [
                    Expression.Constant(instruction.Callee),
                    runtime,
                    arguments
                ])
            : Expression.Call(
                instance: context,
                method: MiContextInvoke,
                arguments:
                [
                    Expression.Constant(instruction.Callee),
                    arguments
                ]);
    }

    private static Expression BuildInstruction(
        VmFunction function,
        VmInstruction instruction,
        LabelTarget[] instructionLabels,
        Expression context,
        Expression locals,
        Expression constants,
        ParameterExpression result,
        LabelTarget returnTarget)
    {
        return instruction switch
        {
            VmMoveInstruction move => BuildOperandWrite(
                locals: locals,
                registerIndex: move.DestinationRegister,
                value: BuildOperandRead(
                    locals: locals,
                    constants: constants,
                    operand: move.Source)),
            VmBinaryInstruction binary => BuildOperandWrite(
                locals: locals,
                registerIndex: binary.DestinationRegister,
                value: BuildBinaryInstruction(
                    instruction: binary,
                    context: context,
                    locals: locals,
                    constants: constants)),
            VmUnaryInstruction unary => BuildOperandWrite(
                locals: locals,
                registerIndex: unary.DestinationRegister,
                value: BuildUnaryInstruction(
                    instruction: unary,
                    context: context,
                    locals: locals,
                    constants: constants)),
            VmLoadIndexInstruction loadIndex => BuildOperandWrite(
                locals: locals,
                registerIndex: loadIndex.DestinationRegister,
                value: Expression.Call(
                    method: MiLoadIndex,
                    arguments:
                    [
                        BuildOperandRead(
                            locals: locals,
                            constants: constants,
                            operand: loadIndex.ArrayOperand),
                        BuildOperandRead(
                            locals: locals,
                            constants: constants,
                            operand: loadIndex.IndexOperand),
                        Expression.Call(
                            instance: context,
                            method: MiRuntime)
                    ])),
            VmStoreIndexInstruction storeIndex => Expression.Call(
                method: MiStoreIndex,
                arguments:
                [
                    BuildOperandRead(
                        locals: locals,
                        constants: constants,
                        operand: storeIndex.ArrayOperand),
                    BuildOperandRead(
                        locals: locals,
                        constants: constants,
                        operand: storeIndex.IndexOperand),
                    BuildOperandRead(
                        locals: locals,
                        constants: constants,
                        operand: storeIndex.ValueOperand),
                    Expression.Call(
                        instance: context,
                        method: MiRuntime)
                ]),
            VmCallInstruction call when call.DestinationRegister is { } destinationRegister => BuildOperandWrite(
                locals: locals,
                registerIndex: destinationRegister,
                value: BuildCallInstruction(
                    instruction: call,
                    context: context,
                    locals: locals,
                    constants: constants)),
            VmCallInstruction call => BuildCallInstruction(
                instruction: call,
                context: context,
                locals: locals,
                constants: constants),
            VmBranchInstruction branch => Expression.Goto(instructionLabels[branch.TargetInstruction]),
            VmBranchConditionInstruction branchCondition => Expression.IfThenElse(
                test: Expression.Call(
                    method: MiToBool,
                    arguments:
                    [
                        BuildOperandRead(
                            locals: locals,
                            constants: constants,
                            operand: branchCondition.Condition),
                        Expression.Call(
                            instance: context,
                            method: MiRuntime)
                    ]),
                ifTrue: Expression.Goto(instructionLabels[branchCondition.TrueTarget]),
                ifFalse: Expression.Goto(instructionLabels[branchCondition.FalseTarget])),
            VmReturnInstruction ret => Expression.Block(
                Expression.Assign(
                    left: result,
                    right: ret.Value is { } operand
                        ? BuildOperandRead(
                            locals: locals,
                            constants: constants,
                            operand: operand)
                        : Expression.Field(
                            expression: null,
                            field: FiVmNull)),
                Expression.Call(
                    instance: context,
                    method: MiContextExit),
                Expression.Return(
                    target: returnTarget,
                    value: result)),
            _ => throw new NotSupportedException(
                $"Instruction '{instruction.GetType().Name}' in '{function.Name}' is not supported by CLR JIT.")
        };
    }

    private static Expression BuildOperandRead(
        Expression locals,
        Expression constants,
        VmOperand operand)
    {
        return operand.Kind switch
        {
            VmOperandKind.Register => Expression.ArrayIndex(
                array: locals,
                index: Expression.Constant(operand.Index)),
            VmOperandKind.Constant => Expression.ArrayIndex(
                array: constants,
                index: Expression.Constant(operand.Index)),
            _ => throw new ArgumentOutOfRangeException(nameof(operand))
        };
    }

    private static Expression BuildOperandWrite(
        Expression locals,
        int registerIndex,
        Expression value)
    {
        return Expression.Assign(
            left: Expression.ArrayAccess(
                array: locals,
                indexes: [Expression.Constant(registerIndex)]),
            right: value);
    }

    private static Expression BuildUnaryInstruction(
        VmUnaryInstruction instruction,
        Expression context,
        Expression locals,
        Expression constants)
    {
        Expression operand = BuildOperandRead(
            locals: locals,
            constants: constants,
            operand: instruction.Operand);

        return instruction.Operation switch
        {
            MUnOp.Neg => Expression.Call(
                method: MiFromLong,
                arguments:
                [
                    Expression.Negate(
                        Expression.Call(MiI64, operand))
                ]),
            MUnOp.Plus => Expression.Call(
                method: MiFromLong,
                arguments:
                [
                    Expression.Call(MiI64, operand)
                ]),
            MUnOp.Not => Expression.Call(
                method: MiFromBool,
                arguments:
                [
                    Expression.Not(
                        Expression.Call(
                            method: MiToBool,
                            arguments:
                            [
                                operand,
                                Expression.Call(
                                    instance: context,
                                    method: MiRuntime)
                            ]))
                ]),
            _ => throw new ArgumentOutOfRangeException(nameof(instruction))
        };
    }

    private static void EmitConstantInitialization(
        ICollection<Expression> expressions,
        VmFunction function,
        Expression context,
        Expression constants)
    {
        for (var constantIndex = 0; constantIndex < function.Constants.Count; constantIndex++)
        {
            expressions.Add(
                BuildOperandWrite(
                    locals: constants,
                    registerIndex: constantIndex,
                    value: BuildVmConstant(
                        constant: function.Constants[constantIndex],
                        context: context)));
        }
    }

    private static void EmitParameterBinding(
        ICollection<Expression> expressions,
        VmFunction function,
        Expression args,
        Expression locals)
    {
        for (var parameterIndex = 0; parameterIndex < function.ParameterRegisters.Count; parameterIndex++)
        {
            expressions.Add(
                BuildOperandWrite(
                    locals: locals,
                    registerIndex: function.ParameterRegisters[parameterIndex],
                    value: Expression.ArrayIndex(
                        array: args,
                        index: Expression.Constant(parameterIndex))));
        }
    }

    private static Expression BuildVmConstant(
        VmConstant constant,
        Expression context)
    {
        return constant.Kind switch
        {
            VmConstantKind.Null => Expression.Field(
                expression: null,
                field: FiVmNull),
            VmConstantKind.I64 => Expression.Call(
                method: MiFromLong,
                arguments: [Expression.Constant(constant.Payload)]),
            VmConstantKind.Bool => Expression.Call(
                method: MiFromBool,
                arguments: [Expression.Constant(constant.Payload != 0)]),
            VmConstantKind.Char => Expression.Call(
                method: MiFromChar,
                arguments: [Expression.Constant((char)constant.Payload)]),
            VmConstantKind.String => Expression.Call(
                instance: Expression.Call(
                    instance: context,
                    method: MiRuntime),
                method: MiAllocateString,
                arguments: [Expression.Constant(constant.Text ?? string.Empty)]),
            _ => throw new ArgumentOutOfRangeException(nameof(constant))
        };
    }
}
