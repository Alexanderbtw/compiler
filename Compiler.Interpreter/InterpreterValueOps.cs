using Compiler.Interpreter.Exceptions;

namespace Compiler.Interpreter;

internal static class InterpreterValueOps
{
    public static bool IsTrue(
        object? value)
    {
        return value switch
        {
            bool boolean => boolean,
            long integer => integer != 0,
            char character => character != '\0',
            string text => text.Length != 0,
            Array array => array.Length != 0,
            null => false,
            _ => true
        };
    }

    public static long ToLong(
        object? value)
    {
        return value switch
        {
            long integer => integer,
            bool boolean => boolean
                ? 1
                : 0,
            null => throw new RuntimeException("null used where integer expected"),
            _ => throw new RuntimeException($"cannot use {value.GetType().Name} as integer")
        };
    }
}
