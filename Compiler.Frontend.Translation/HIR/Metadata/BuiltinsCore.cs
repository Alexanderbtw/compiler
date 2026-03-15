namespace Compiler.Frontend.Translation.HIR.Metadata;

/// <summary>
///     Compatibility facade over the shared builtin output sink.
/// </summary>
public static class BuiltinsCore
{
    /// <summary>
    ///     Writes one formatted builtin output line.
    /// </summary>
    /// <param name="tokens">Rendered tokens.</param>
    public static void PrintLine(
        IEnumerable<string> tokens)
    {
        Compiler.Core.Builtins.BuiltinsCore.PrintLine(tokens);
    }

    /// <summary>
    ///     Overrides the builtin output writer for the current async flow.
    /// </summary>
    /// <param name="writer">Writer to use.</param>
    /// <returns>Scope that restores the previous writer.</returns>
    public static IDisposable PushWriter(
        TextWriter writer)
    {
        return Compiler.Core.Builtins.BuiltinsCore.PushWriter(writer);
    }
}
