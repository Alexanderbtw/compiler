namespace Compiler.Backend.VM;

public enum OpCode
{
    // consts / locals
    LdcI64, LdcBool, LdcChar, LdcStr, LdcNull,
    LdLoc, StLoc, Pop,

    // arithmetic / logic
    Add, Sub, Mul, Div, Mod,
    Lt, Le, Gt, Ge, Eq, Ne,
    Neg, Not,

    // arrays
    LdElem, StElem,

    // control flow
    Br, BrTrue, Ret,

    // calls
    CallUser, // A = funcIndex, B = argc
    CallBuiltin, // A = stringId,  B = argc
    NewArr,
    Len
}
