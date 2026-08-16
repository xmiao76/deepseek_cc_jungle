namespace JungleGame.Bench;

/// <summary>Simple name/value command-line argument helpers shared by all modes.</summary>
internal static class Args
{
    internal static int ReadInt(string[] args, string name, int fallback)
    {
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (args[i] == name && int.TryParse(args[i + 1], out int value))
                return value;
        }
        return fallback;
    }

    internal static int? TryReadInt(string[] args, string name)
    {
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (args[i] == name && int.TryParse(args[i + 1], out int value))
                return value;
        }
        return null;
    }

    internal static string? ReadString(string[] args, string name) =>
        ReadString(args, name, null);

    internal static string? ReadString(string[] args, string name, string? fallback)
    {
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (args[i] == name && !args[i + 1].StartsWith("--", StringComparison.Ordinal))
                return args[i + 1];
        }
        return fallback;
    }

    /// <summary>
    /// Data files (suite/bench files) are copied to the output directory; when
    /// invoked via "dotnet run" the working directory is the repo root, so fall
    /// back to the executable's directory for relative paths.
    /// </summary>
    internal static string ResolveDataPath(string path) =>
        File.Exists(path) || Path.IsPathRooted(path)
            ? path
            : Path.Combine(AppContext.BaseDirectory, path);
}
