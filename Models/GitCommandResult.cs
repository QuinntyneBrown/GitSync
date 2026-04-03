namespace GitSync.Models;

public sealed record GitCommandResult(
    int ExitCode,
    string Stdout,
    string Stderr,
    bool TimedOut)
{
    public bool Succeeded => ExitCode == 0 && !TimedOut;
}
