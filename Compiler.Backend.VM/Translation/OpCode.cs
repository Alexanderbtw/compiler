namespace Compiler.Backend.VM.Translation;

public enum OpCode
{
    // Constants and locals
    LdcI64, // Push 64-bit integer constant onto the operand stack (Imm holds the value)
    LdcBool, // Push boolean constant (Imm: 0 = false, 1 = true)
    LdcChar, // Push character constant (Imm: UTF-16 code unit)
    LdcStr, // Push string from the module's string pool (Idx: pool index)
    LdcNull, // Push null
    LdLoc, // Push the value of local variable at slot A
    StLoc, // Pop and store into local variable at slot A
    Pop, // Pop and discard the top value of the operand stack

    // Arithmetic / logic (binary ops pop Right then Left, then push the result)
    Add, // Int64 addition:       push (Left + Right)
    Sub, // Int64 subtraction:    push (Left - Right)
    Mul, // Int64 multiplication: push (Left * Right)
    Div, // Int64 division:       push (Left / Right); throws on division by zero
    Mod, // Int64 modulo:         push (Left % Right); throws on division by zero
    Lt, // Int64 compare:        push (Left <  Right) as bool
    Le, // Int64 compare:        push (Left <= Right) as bool
    Gt, // Int64 compare:        push (Left >  Right) as bool
    Ge, // Int64 compare:        push (Left >= Right) as bool
    Eq, // Equality per VM semantics (type-aware; strings by value, arrays by reference); push bool
    Ne, // Inequality per VM semantics; push bool
    Neg, // Unary numeric negation: push (-Value)
    Not, // Logical NOT: coerce popped value to bool, then invert

    // Arrays
    LdElem, // Pop index (int), then array; push array[index]
    StElem, // Pop value, then index, then array; perform array[index] = value

    // Control flow
    Br, // Unconditional branch: set PC = A
    BrTrue, // Conditional branch: pop condition → bool; if true set PC = A, else fall through
    Ret, // Return: pop return value; if no caller, exit VM; otherwise push into caller and resume

    // Calls and fast builtins
    CallUser, // Call user function (A = function index, B = arg count); pops args; pushes result
    CallBuiltin, // Call builtin by name (A = string-pool index, B = arg count); pops args; pushes result
    NewArr, // Allocate new array: pop length (int64), push new array
    Len // Length: pop value; push length of string/array (error for other types)
}
