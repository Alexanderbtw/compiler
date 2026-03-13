namespace Compiler.Frontend.Translation.HIR.Metadata;

/// <summary>
///     Shared sink for runtime printing.
///     Supports scoped writer overrides so tests and hosts do not need to mutate Console.Out.
/// </summary>
public static class BuiltinsCore
{
    private static readonly AsyncLocal<TextWriter?> CurrentWriter = new AsyncLocal<TextWriter?>();

    public static void PrintLine(
        IEnumerable<string> tokens)
    {
        TextWriter writer = CurrentWriter.Value ?? Console.Out;
        writer.WriteLine(
            string.Join(
                separator: " ",
                values: tokens));
    }

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
