namespace Compiler.Frontend.Translation.HIR.Metadata;

/// <summary>
///     Minimal shared sink for printing.
///     Used by the interpreter and JIT builtins to route output consistently.
/// </summary>
public static class BuiltinsCore
{
    public static void PrintLine(
        IEnumerable<string> tokens)
    {
        Console.WriteLine(
            string.Join(
                separator: " ",
                values: tokens));
    }
}
