using GitSync.Models;
using Microsoft.Extensions.Logging;

namespace GitSync.Services;

public sealed class GitService : IGitService
{
    private readonly IGitProcessRunner _runner;
    private readonly SyncOptions       _options;
    private readonly ILogger<GitService> _logger;

    private string? _gitPath;

    public GitService(
        IGitProcessRunner runner,
        SyncOptions options,
        ILogger<GitService> logger)
    {
        _runner  = runner;
        _options = options;
        _logger  = logger;
    }

    // -------------------------------------------------------------------------
    // Git executable discovery
    // -------------------------------------------------------------------------

    public Task<string?> FindGitExecutableAsync()
    {
        if (_gitPath is not null)
            return Task.FromResult<string?>(_gitPath);

        var pathVar = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        var separator = System.IO.Path.PathSeparator;
        var candidates = OperatingSystem.IsWindows()
            ? new[] { "git.exe", "git" }
            : new[] { "git" };

        foreach (var dir in pathVar.Split(separator, StringSplitOptions.RemoveEmptyEntries))
        {
            foreach (var name in candidates)
            {
                var full = System.IO.Path.Combine(dir, name);
                if (File.Exists(full))
                {
                    _gitPath = full;
                    _logger.LogDebug("Found git executable: {Path}", full);
                    return Task.FromResult<string?>(full);
                }
            }
        }

        return Task.FromResult<string?>(null);
    }

    // -------------------------------------------------------------------------
    // Repository validation
    // -------------------------------------------------------------------------

    public async Task<bool> ValidateRepositoryAsync(
        DirectoryInfo path,
        CancellationToken cancellationToken = default)
    {
        if (!path.Exists)
            return false;

        var gitPath = await FindGitExecutableAsync();
        if (gitPath is null)
            return false;

        var result = await _runner.RunAsync(
            BuildOptions(["rev-parse", "--is-inside-work-tree"], path.FullName, gitPath),
            cancellationToken);

        return result.Succeeded && result.Stdout.Trim().Equals("true", StringComparison.OrdinalIgnoreCase);
    }

    // -------------------------------------------------------------------------
    // Per-iteration operations
    // -------------------------------------------------------------------------

    public async Task<GitCommandResult> PullAsync(CancellationToken cancellationToken = default)
    {
        var result = await _runner.RunAsync(BuildOptions(["pull"]), cancellationToken);
        if (!result.Succeeded)
            _logger.LogWarning("git pull failed (exit {Code}): {Stderr}", result.ExitCode, result.Stderr.Trim());
        else
            _logger.LogDebug("git pull: {Stdout}", result.Stdout.Trim());
        return result;
    }

    public async Task<bool> DetectChangesAsync(CancellationToken cancellationToken = default)
    {
        var result = await _runner.RunAsync(BuildOptions(["status", "--porcelain"]), cancellationToken);
        if (!result.Succeeded)
        {
            _logger.LogWarning("git status failed (exit {Code}): {Stderr}", result.ExitCode, result.Stderr.Trim());
            return false;
        }
        return result.Stdout.Trim().Length > 0;
    }

    public async Task<GitCommandResult> StageAsync(CancellationToken cancellationToken = default)
    {
        var result = await _runner.RunAsync(BuildOptions(["add", "-A"]), cancellationToken);
        if (!result.Succeeded)
            _logger.LogError("git add -A failed (exit {Code}): {Stderr}", result.ExitCode, result.Stderr.Trim());
        return result;
    }

    public async Task<GitCommandResult> CommitAsync(string message, CancellationToken cancellationToken = default)
    {
        var result = await _runner.RunAsync(BuildOptions(["-c", "user.email=gitsync@local", "-c", "user.name=GitSync", "commit", "-m", message]), cancellationToken);
        if (!result.Succeeded)
            _logger.LogWarning("git commit failed (exit {Code}): {Stderr}", result.ExitCode, result.Stderr.Trim());
        return result;
    }

    public async Task<GitCommandResult> PushAsync(CancellationToken cancellationToken = default)
    {
        var result = await _runner.RunAsync(BuildOptions(["push"]), cancellationToken);

        if (!result.Succeeded && result.Stderr.Contains("no upstream", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogDebug("No upstream configured — retrying with 'git push origin HEAD'");
            result = await _runner.RunAsync(BuildOptions(["push", "origin", "HEAD"]), cancellationToken);
        }

        if (!result.Succeeded)
            _logger.LogError("git push failed (exit {Code}): {Stderr}", result.ExitCode, result.Stderr.Trim());
        else
            _logger.LogInformation("Push succeeded");

        return result;
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private GitCommandOptions BuildOptions(
        IReadOnlyList<string> args,
        string? workingDirectory = null,
        string? gitPath = null) =>
        new(
            GitExecutablePath: gitPath ?? _gitPath ?? "git",
            Arguments: args,
            WorkingDirectory: workingDirectory ?? _options.Path.FullName,
            Timeout: _options.GitTimeout);
}
