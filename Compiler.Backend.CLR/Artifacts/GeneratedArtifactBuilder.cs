using System.Diagnostics;
using System.Text;

namespace Compiler.Backend.CLR.Artifacts;

internal static class GeneratedArtifactBuilder
{
    public static string FindRepositoryRoot()
    {
        string? overrideRoot = Environment.GetEnvironmentVariable("MINILANG_REPO_ROOT");

        if (!string.IsNullOrWhiteSpace(overrideRoot) &&
            File.Exists(Path.Combine(overrideRoot, "Compiler.sln")))
        {
            return overrideRoot;
        }

        string directory = AppContext.BaseDirectory;

        while (!string.IsNullOrWhiteSpace(directory))
        {
            if (File.Exists(Path.Combine(directory, "Compiler.sln")))
            {
                return directory;
            }

            DirectoryInfo? parent = Directory.GetParent(directory);
            if (parent is null)
            {
                break;
            }

            directory = parent.FullName;
        }

        throw new InvalidOperationException("Failed to locate the repository root for generated artifact compilation.");
    }

    public static GeneratedClrArtifact Build(
        ClrArtifactOptions options,
        IReadOnlyDictionary<string, string> files)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(files);
        ValidateOptions(options);

        Directory.CreateDirectory(options.OutputDirectory);

        foreach ((string relativePath, string content) in files)
        {
            string fullPath = Path.Combine(
                options.OutputDirectory,
                relativePath);

            Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
            File.WriteAllText(
                path: fullPath,
                contents: content,
                encoding: Encoding.UTF8);
        }

        string projectFilePath = Path.Combine(
            options.OutputDirectory,
            $"{options.AssemblyName}.csproj");

        RunDotnetBuild(
            projectFilePath: projectFilePath,
            configuration: options.Configuration,
            workingDirectory: options.OutputDirectory);

        string buildDirectory = Path.Combine(
            options.OutputDirectory,
            "bin",
            options.Configuration,
            options.TargetFramework);

        string assemblyPath = Path.Combine(buildDirectory, $"{options.AssemblyName}.dll");
        string depsFilePath = Path.Combine(buildDirectory, $"{options.AssemblyName}.deps.json");
        string runtimeConfigPath = Path.Combine(buildDirectory, $"{options.AssemblyName}.runtimeconfig.json");

        EnsureBuildArtifactExists(assemblyPath);
        EnsureBuildArtifactExists(depsFilePath);
        EnsureBuildArtifactExists(runtimeConfigPath);

        return new GeneratedClrArtifact(
            projectDirectory: options.OutputDirectory,
            projectFilePath: projectFilePath,
            assemblyPath: assemblyPath,
            depsFilePath: depsFilePath,
            runtimeConfigPath: runtimeConfigPath);
    }

    public static string EscapeString(
        string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        return value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal)
            .Replace("\r", "\\r", StringComparison.Ordinal)
            .Replace("\n", "\\n", StringComparison.Ordinal)
            .Replace("\t", "\\t", StringComparison.Ordinal);
    }

    public static string ToSafeIdentifier(
        string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);

        var builder = new StringBuilder(value.Length);

        foreach (char symbol in value)
        {
            builder.Append(char.IsLetterOrDigit(symbol) || symbol == '_'
                ? symbol
                : '_');
        }

        if (!char.IsLetter(builder[0]) && builder[0] != '_')
        {
            builder.Insert(0, '_');
        }

        return builder.ToString();
    }

    public static void ValidateEntryFunction(
        string entryFunctionName,
        IEnumerable<string> availableFunctions)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(entryFunctionName);
        ArgumentNullException.ThrowIfNull(availableFunctions);

        if (availableFunctions.Contains(
                value: entryFunctionName,
                comparer: StringComparer.Ordinal))
        {
            return;
        }

        throw new InvalidOperationException($"entry '{entryFunctionName}' not found in the source module");
    }

    private static void EnsureBuildArtifactExists(
        string path)
    {
        if (File.Exists(path))
        {
            return;
        }

        throw new InvalidOperationException($"Expected generated artifact '{path}' was not produced.");
    }

    private static void ValidateOptions(
        ClrArtifactOptions options)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(options.AssemblyName);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.Configuration);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.OutputDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.TargetFramework);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.EntryFunctionName);
    }

    private static void RunDotnetBuild(
        string projectFilePath,
        string configuration,
        string workingDirectory)
    {
        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "dotnet",
                WorkingDirectory = workingDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            }
        };

        process.StartInfo.ArgumentList.Add("build");
        process.StartInfo.ArgumentList.Add(projectFilePath);
        process.StartInfo.ArgumentList.Add("-c");
        process.StartInfo.ArgumentList.Add(configuration);
        process.StartInfo.ArgumentList.Add("-nologo");

        process.Start();
        string standardOutput = process.StandardOutput.ReadToEnd();
        string standardError = process.StandardError.ReadToEnd();
        process.WaitForExit();

        if (process.ExitCode == 0)
        {
            return;
        }

        throw new InvalidOperationException(
            $"Failed to build generated artifact '{projectFilePath}'.{Environment.NewLine}{standardOutput}{Environment.NewLine}{standardError}");
    }
}
