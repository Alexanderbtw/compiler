using Compiler.Frontend.Translation.MIR.Instructions.Abstractions;

namespace Compiler.Runtime.VM.Bytecode;

public abstract record VmInstruction;

public sealed record VmMoveInstruction(
    int DestinationRegister,
    VmOperand Source) : VmInstruction;

public sealed record VmBinaryInstruction(
    int DestinationRegister,
    MBinOp Operation,
    VmOperand Left,
    VmOperand Right) : VmInstruction;

public sealed record VmUnaryInstruction(
    int DestinationRegister,
    MUnOp Operation,
    VmOperand Operand) : VmInstruction;

public sealed record VmLoadIndexInstruction(
    int DestinationRegister,
    VmOperand ArrayOperand,
    VmOperand IndexOperand) : VmInstruction;

public sealed record VmStoreIndexInstruction(
    VmOperand ArrayOperand,
    VmOperand IndexOperand,
    VmOperand ValueOperand) : VmInstruction;

public sealed record VmCallInstruction(
    int? DestinationRegister,
    string Callee,
    IReadOnlyList<VmOperand> Arguments) : VmInstruction;

public sealed record VmBranchInstruction(
    int TargetInstruction) : VmInstruction;

public sealed record VmBranchConditionInstruction(
    VmOperand Condition,
    int TrueTarget,
    int FalseTarget) : VmInstruction;

public sealed record VmReturnInstruction(
    VmOperand? Value) : VmInstruction;
