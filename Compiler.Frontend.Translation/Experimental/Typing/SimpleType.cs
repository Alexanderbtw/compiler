namespace Compiler.Frontend.Translation.Experimental.Typing;

/// <summary>
///     Experimental type sketch used only by typing WIP and builtin metadata.
///     It is intentionally isolated until the language gets a committed type model.
/// </summary>
public enum SimpleType
{
    Unknown,
    Int,
    Bool,
    Char,
    String,

    Void
    /*, Array(T), ... */
}
