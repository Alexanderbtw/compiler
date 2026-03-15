namespace Compiler.Core.Builtins;

/// <summary>
///     Shared sink for builtin printing with scoped writer overrides.
/// </summary>
public static class BuiltinsCore
{
    private static readonly AsyncLocal<TextWriter?> CurrentWriter = new();

    /// <summary>
    ///     Writes one formatted builtin output line.
    /// </summary>
    /// <param name="tokens">Rendered tokens.</param>
    public static void PrintLine(
        IEnumerable<string> tokens)
    {
        TextWriter writer = CurrentWriter.Value ?? Console.Out;
        writer.WriteLine(
            string.Join(
                separator: " ",
                values: tokens));
    }

    /// <summary>
    ///     Overrides the builtin output writer for the current async flow.
    /// </summary>
    /// <param name="writer">Writer to use.</param>
    /// <returns>Scope that restores the previous writer.</returns>
    public static IDisposable PushWriter(
        TextWriter writer)
    {
        ArgumentNullException.ThrowIfNull(writer);

        TextWriter? previous = CurrentWriter.Value;
        CurrentWriter.Value = writer;

        return new WriterScope(previous);
    }

    private sealed class WriterScope(
        TextWriter? previous) : IDisposable
    {
        public void Dispose()
        {
            CurrentWriter.Value = previous;
        }
    }
}
