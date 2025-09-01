using Compiler.Frontend.Translation.HIR.Common;
using Compiler.Frontend.Translation.HIR.Expressions;
using Compiler.Frontend.Translation.HIR.Expressions.Abstractions;
using Compiler.Frontend.Translation.HIR.Statements;
using Compiler.Frontend.Translation.HIR.Statements.Abstractions;
using Compiler.Frontend.Translation.MIR.Common;
using Compiler.Frontend.Translation.MIR.Instructions;
using Compiler.Frontend.Translation.MIR.Instructions.Abstractions;
using Compiler.Frontend.Translation.MIR.Operands;

namespace Compiler.Frontend.Translation.MIR;

public sealed class HirToMir
{
    public MirModule Lower(
        ProgramHir program)
    {
        var context = new LoweringContext();

        foreach (FuncHir functionHir in program.Functions)
        {
            context.Module.Functions.Add(
                LowerFunction(
                    context: context,
                    function: functionHir));
        }

        return context.Module;
    }

    private MOperand LowerExpression(
        LoweringContext context,
        ExprHir expr)
    {
        switch (expr)
        {
            case IntHir intLiteral:
                return new Const(intLiteral.Value);
            case BoolHir boolLiteral:
                return new Const(boolLiteral.Value);
            case CharHir charLiteral:
                return new Const(charLiteral.Value);
            case StringHir stringLiteral:
                return new Const(stringLiteral.Value);

            case VarHir varExpr:
                if (!context.TryGetVariableRegister(
                        name: varExpr.Name,
                        reg: out VReg? variableReg))
                {
                    throw new InvalidOperationException($"variable '{varExpr.Name}' not in scope during MIR lowering");
                }

                return variableReg!;

            case UnHir unary:
                {
                    MOperand operand = LowerExpression(
                        context: context,
                        expr: unary.Operand);

                    VReg destination = context.CurrentFunction.NewTemp();
                    context.CurrentBlock.Instructions.Add(
                        new Un(
                            Dst: destination,
                            Op: MapUn(unary.Op),
                            X: operand));

                    return destination;
                }

            case BinHir { Op: BinOp.Assign } assignment:
                {
                    MOperand rightValue = LowerExpression(
                        context: context,
                        expr: assignment.Right);

                    switch (assignment.Left)
                    {
                        case VarHir leftVar when context.TryGetVariableRegister(
                            name: leftVar.Name,
                            reg: out VReg? leftReg):
                            context.CurrentBlock.Instructions.Add(
                                new Move(
                                    Dst: leftReg!,
                                    Src: rightValue));

                            return leftReg!;

                        case IndexHir leftIndex:
                            {
                                MOperand arrayOperand = LowerExpression(
                                    context: context,
                                    expr: leftIndex.Target);

                                MOperand indexOperand = LowerExpression(
                                    context: context,
                                    expr: leftIndex.Index);

                                context.CurrentBlock.Instructions.Add(
                                    new StoreIndex(
                                        Arr: arrayOperand,
                                        Index: indexOperand,
                                        Value: rightValue));

                                return rightValue;
                            }

                        default:
                            throw new NotSupportedException("assignment target not supported");
                    }
                }

            case BinHir { Op: BinOp.And or BinOp.Or } shortCircuit:
                return LowerShortCircuitBinary(
                    context: context,
                    bin: shortCircuit);

            case BinHir binary:
                {
                    MOperand leftOperand = LowerExpression(
                        context: context,
                        expr: binary.Left);

                    MOperand rightOperand = LowerExpression(
                        context: context,
                        expr: binary.Right);

                    VReg destination = context.CurrentFunction.NewTemp();
                    context.CurrentBlock.Instructions.Add(
                        new Bin(
                            Dst: destination,
                            Op: MapBin(binary.Op),
                            L: leftOperand,
                            R: rightOperand));

                    return destination;
                }

            case CallHir call:
                {
                    string calleeName = (call.Callee as VarHir)?.Name
                        ?? throw new NotSupportedException("only simple function names are callable");

                    List<MOperand> loweredArgs = call
                        .Args
                        .Select(a => LowerExpression(
                            context: context,
                            expr: a))
                        .ToList();

                    VReg destination = context.CurrentFunction.NewTemp();
                    context.CurrentBlock.Instructions.Add(
                        new Call(
                            Dst: destination,
                            Callee: calleeName,
                            Args: loweredArgs));

                    return destination;
                }

            case IndexHir indexExpr:
                {
                    MOperand arrayOperand = LowerExpression(
                        context: context,
                        expr: indexExpr.Target);

                    MOperand indexOperand = LowerExpression(
                        context: context,
                        expr: indexExpr.Index);

                    VReg destination = context.CurrentFunction.NewTemp();
                    context.CurrentBlock.Instructions.Add(
                        new LoadIndex(
                            Dst: destination,
                            Arr: arrayOperand,
                            Index: indexOperand));

                    return destination;
                }

            default:
                throw new NotSupportedException($"Expr {expr.GetType().Name} not supported in MIR lowering");
        }
    }

    private MirFunction LowerFunction(
        LoweringContext context,
        FuncHir function)
    {
        context.CurrentFunction = new MirFunction(function.Name);
        context.CurrentBlock = context.CurrentFunction.NewBlock("entry");

        context.PushScope();

        foreach (string parameterName in function.Parameters)
        {
            VReg parameterReg = context.DefineVariable(parameterName);
            context.CurrentFunction.ParamNames.Add(parameterName);
            context.CurrentFunction.ParamRegs.Add(parameterReg);
        }

        LowerStatement(
            context: context,
            stmt: function.Body);

        // Гарантируем, что у последнего активного блока есть терминатор
        if (context.CurrentBlock.Terminator is null)
        {
            context.CurrentBlock.Terminator = new Ret(null);
        }

        context.PopScope();

        return context.CurrentFunction;
    }

    private MOperand LowerShortCircuitBinary(
        LoweringContext context,
        BinHir bin)
    {
        VReg resultTemp = context.CurrentFunction.NewTemp();

        MOperand leftEvaluated = LowerExpression(
            context: context,
            expr: bin.Left);

        MirBlock rightBlock = context.CurrentFunction.NewBlock($"sc_rhs_{context.CurrentFunction.Blocks.Count}");
        MirBlock joinBlock = context.CurrentFunction.NewBlock($"sc_join_{context.CurrentFunction.Blocks.Count}");
        MirBlock shortBlock = context.CurrentFunction.NewBlock($"sc_short_{context.CurrentFunction.Blocks.Count}");

        if (bin.Op == BinOp.And)
        {
            // if (!L) goto short; else goto rhs
            context.CurrentBlock.Terminator = new BrCond(
                Cond: leftEvaluated,
                IfTrue: rightBlock,
                IfFalse: shortBlock);

            // short: result = false
            context.CurrentBlock = shortBlock;
            context.CurrentBlock.Instructions.Add(
                new Move(
                    Dst: resultTemp,
                    Src: new Const(false)));

            context.CurrentBlock.Terminator = new Br(joinBlock);

            // rhs: result = R
            context.CurrentBlock = rightBlock;
            MOperand rightEvaluated = LowerExpression(
                context: context,
                expr: bin.Right);

            context.CurrentBlock.Instructions.Add(
                new Move(
                    Dst: resultTemp,
                    Src: rightEvaluated));

            context.CurrentBlock.Terminator = new Br(joinBlock);
        }
        else // OR
        {
            // if (L) goto short; else goto rhs
            context.CurrentBlock.Terminator = new BrCond(
                Cond: leftEvaluated,
                IfTrue: shortBlock,
                IfFalse: rightBlock);

            // short: result = true
            context.CurrentBlock = shortBlock;
            context.CurrentBlock.Instructions.Add(
                new Move(
                    Dst: resultTemp,
                    Src: new Const(true)));

            context.CurrentBlock.Terminator = new Br(joinBlock);

            // rhs: result = R
            context.CurrentBlock = rightBlock;
            MOperand rightEvaluated = LowerExpression(
                context: context,
                expr: bin.Right);

            context.CurrentBlock.Instructions.Add(
                new Move(
                    Dst: resultTemp,
                    Src: rightEvaluated));

            context.CurrentBlock.Terminator = new Br(joinBlock);
        }

        context.CurrentBlock = joinBlock;

        return resultTemp;
    }

    private void LowerStatement(
        LoweringContext context,
        StmtHir stmt)
    {
        switch (stmt)
        {
            case BlockHir block:
                context.PushScope();

                foreach (StmtHir innerStmt in block.Statements)
                {
                    LowerStatement(
                        context: context,
                        stmt: innerStmt);
                }

                context.PopScope();

                break;

            case LetHir letStmt:
                {
                    VReg? destination = context.DefineVariable(letStmt.Name);

                    if (letStmt.Init is not null)
                    {
                        MOperand source = LowerExpression(
                            context: context,
                            expr: letStmt.Init);

                        context.CurrentBlock.Instructions.Add(
                            new Move(
                                Dst: destination,
                                Src: source));
                    }

                    break;
                }

            case ExprStmtHir exprStmt:
                if (exprStmt.Expr is not null)
                {
                    _ = LowerExpression(
                        context: context,
                        expr: exprStmt.Expr);
                }

                break;

            case ReturnHir returnStmt:
                {
                    MOperand? returnValue = returnStmt.Expr is null
                        ? null
                        : LowerExpression(
                            context: context,
                            expr: returnStmt.Expr);

                    context.CurrentBlock.Terminator = new Ret(returnValue);
                    context.CurrentBlock = context.CurrentFunction.NewBlock($"dead_{context.CurrentFunction.Blocks.Count}");

                    break;
                }

            case IfHir ifStmt:
                {
                    MirBlock thenBlock = context.CurrentFunction.NewBlock($"then_{context.CurrentFunction.Blocks.Count}");
                    MirBlock elseBlock = context.CurrentFunction.NewBlock($"else_{context.CurrentFunction.Blocks.Count}");
                    MirBlock joinBlock = context.CurrentFunction.NewBlock($"join_{context.CurrentFunction.Blocks.Count}");

                    MOperand condition = LowerExpression(
                        context: context,
                        expr: ifStmt.Cond);

                    context.CurrentBlock.Terminator = new BrCond(
                        Cond: condition,
                        IfTrue: thenBlock,
                        IfFalse: ifStmt.Else is null
                            ? joinBlock
                            : elseBlock);

                    // then
                    context.CurrentBlock = thenBlock;
                    LowerStatement(
                        context: context,
                        stmt: ifStmt.Then);

                    if (context.CurrentBlock.Terminator is null)
                    {
                        context.CurrentBlock.Terminator = new Br(joinBlock);
                    }

                    // else
                    if (ifStmt.Else is not null)
                    {
                        context.CurrentBlock = elseBlock;
                        LowerStatement(
                            context: context,
                            stmt: ifStmt.Else);

                        if (context.CurrentBlock.Terminator is null)
                        {
                            context.CurrentBlock.Terminator = new Br(joinBlock);
                        }
                    }

                    // continue at join
                    context.CurrentBlock = joinBlock;

                    break;
                }

            case WhileHir whileStmt:
                {
                    MirBlock headBlock = context.CurrentFunction.NewBlock($"head_{context.CurrentFunction.Blocks.Count}");
                    MirBlock bodyBlock = context.CurrentFunction.NewBlock($"body_{context.CurrentFunction.Blocks.Count}");
                    MirBlock exitBlock = context.CurrentFunction.NewBlock($"exit_{context.CurrentFunction.Blocks.Count}");

                    context.CurrentBlock.Terminator = new Br(headBlock);

                    context.CurrentBlock = headBlock;
                    MOperand condition = LowerExpression(
                        context: context,
                        expr: whileStmt.Cond);

                    context.CurrentBlock.Terminator = new BrCond(
                        Cond: condition,
                        IfTrue: bodyBlock,
                        IfFalse: exitBlock);

                    context.LoopTargets.Push((exitBlock, headBlock));

                    context.CurrentBlock = bodyBlock;
                    LowerStatement(
                        context: context,
                        stmt: whileStmt.Body);

                    if (context.CurrentBlock.Terminator is null)
                    {
                        context.CurrentBlock.Terminator = new Br(headBlock);
                    }

                    context.LoopTargets.Pop();
                    context.CurrentBlock = exitBlock;

                    break;
                }

            case BreakHir:
                {
                    (MirBlock breakTarget, _) = context.LoopTargets.Peek();
                    context.CurrentBlock.Terminator = new Br(breakTarget);
                    context.CurrentBlock = context.CurrentFunction.NewBlock($"dead_{context.CurrentFunction.Blocks.Count}");

                    break;
                }

            case ContinueHir:
                {
                    (_, MirBlock continueTarget) = context.LoopTargets.Peek();
                    context.CurrentBlock.Terminator = new Br(continueTarget);
                    context.CurrentBlock = context.CurrentFunction.NewBlock($"dead_{context.CurrentFunction.Blocks.Count}");

                    break;
                }

            default:
                throw new NotSupportedException($"Stmt {stmt.GetType().Name} not supported in MIR lowering");
        }
    }

    private static MBinOp MapBin(
        BinOp op)
    {
        return op switch
        {
            BinOp.Add => MBinOp.Add, BinOp.Sub => MBinOp.Sub, BinOp.Mul => MBinOp.Mul,
            BinOp.Div => MBinOp.Div, BinOp.Mod => MBinOp.Mod,
            BinOp.Lt => MBinOp.Lt, BinOp.Le => MBinOp.Le, BinOp.Gt => MBinOp.Gt, BinOp.Ge => MBinOp.Ge,
            BinOp.Eq => MBinOp.Eq, BinOp.Ne => MBinOp.Ne,
            _ => throw new NotSupportedException($"bin op {op} not supported in MIR")
        };
    }

    private static MUnOp MapUn(
        UnOp op)
    {
        return op switch
        {
            UnOp.Neg => MUnOp.Neg, UnOp.Not => MUnOp.Not, UnOp.Plus => MUnOp.Plus,
            _ => throw new NotSupportedException($"un op {op} not supported in MIR")
        };
    }

    private sealed class LoweringContext
    {
        public MirBlock CurrentBlock = null!;
        public MirFunction CurrentFunction = null!;
        public readonly Stack<(MirBlock BreakTarget, MirBlock ContinueTarget)> LoopTargets = new Stack<(MirBlock BreakTarget, MirBlock ContinueTarget)>();
        public readonly MirModule Module = new MirModule();
        public readonly Stack<Dictionary<string, VReg?>> Scopes = new Stack<Dictionary<string, VReg?>>();

        public VReg DefineVariable(
            string name)
        {
            VReg reg = CurrentFunction.NewTemp();
            Scopes
                .Peek()[name] = reg;

            return reg;
        }

        public void PopScope()
        {
            Scopes.Pop();
        }

        public void PushScope()
        {
            Scopes.Push(new Dictionary<string, VReg?>());
        }

        public bool TryGetVariableRegister(
            string name,
            out VReg? reg)
        {
            foreach (Dictionary<string, VReg?> scope in Scopes)
            {
                if (scope.TryGetValue(
                        key: name,
                        value: out reg))
                {
                    return true;
                }
            }

            reg = null!;

            return false;
        }
    }
}
