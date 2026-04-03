namespace GitSync.Models;

public sealed record GitCommandOptions(
    string GitExecutablePath,
    IReadOnlyList<string> Arguments,
    string WorkingDirectory,
    TimeSpan Timeout);
