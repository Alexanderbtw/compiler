namespace Compiler.Core.Operations;

/// <summary>
///     Runtime/backend-neutral binary operations used by bytecode execution tiers.
/// </summary>
public enum MBinOp
{
    Add,
    Sub,
    Mul,
    Div,
    Mod,
    Lt,
    Le,
    Gt,
    Ge,
    Eq,
    Ne
}
