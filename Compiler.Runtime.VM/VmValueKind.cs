namespace Compiler.Runtime.VM;

/// <summary>
///     Execution value discriminant for the register VM.
/// </summary>
public enum VmValueKind
{
    Null,
    I64,
    Bool,
    Char,
    Ref
}
