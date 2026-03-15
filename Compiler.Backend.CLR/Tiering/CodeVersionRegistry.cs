namespace Compiler.Backend.CLR.Tiering;

/// <summary>
///     Stores per-function hotness and active execution target metadata.
/// </summary>
public sealed class CodeVersionRegistry
{
    private readonly Dictionary<string, FunctionProfile> _profiles = new(StringComparer.Ordinal);

    /// <summary>
    ///     Enumerates tracked function profiles.
    /// </summary>
    public IReadOnlyCollection<FunctionProfile> Profiles => _profiles.Values;

    /// <summary>
    ///     Gets an existing profile or creates a new one.
    /// </summary>
    /// <param name="functionName">Function name.</param>
    /// <returns>Tracked profile.</returns>
    public FunctionProfile GetOrAdd(
        string functionName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(functionName);

        if (_profiles.TryGetValue(
                key: functionName,
                value: out FunctionProfile? profile))
        {
            return profile;
        }

        profile = new FunctionProfile(functionName);
        _profiles[functionName] = profile;

        return profile;
    }
}
