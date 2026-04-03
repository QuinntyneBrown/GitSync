using System.Diagnostics;
using GitSync.Models;
using Microsoft.Extensions.Logging;

namespace GitSync.Services;

public sealed class GitProcessRunner : IGitProcessRunner
{
    private readonly ILogger<GitProcessRunner> _logger;

    public GitProcessRunner(ILogger<GitProcessRunner> logger)
    {
        _logger = logger;
    }

    public async Task<GitCommandResult> RunAsync(
        GitCommandOptions options,
        CancellationToken cancellationToken = default)
    {
        var psi = new ProcessStartInfo
        {
            FileName               = options.GitExecutablePath,
            WorkingDirectory       = options.WorkingDirectory,
            UseShellExecute        = false,   // NEVER true — prevents shell injection
            RedirectStandardOutput = true,
            RedirectStandardError  = true,
            CreateNoWindow         = true,
        };

        // Use ArgumentList (not Arguments string) so each arg is passed verbatim — no shell quoting
        foreach (var arg in options.Arguments)
            psi.ArgumentList.Add(arg);

        _logger.LogDebug("Running: {Git} {Args} in {Dir}",
            options.GitExecutablePath,
            string.Join(" ", options.Arguments),
            options.WorkingDirectory);

        using var process = new Process { StartInfo = psi };
        process.Start();

        var stdoutTask = process.StandardOutput.ReadToEndAsync();
        var stderrTask = process.StandardError.ReadToEndAsync();

        using var timeoutCts = new CancellationTokenSource(options.Timeout);
        using var linkedCts  = CancellationTokenSource.CreateLinkedTokenSource(
                                   timeoutCts.Token, cancellationToken);

        try
        {
            await process.WaitForExitAsync(linkedCts.Token);
        }
        catch (OperationCanceledException)
        {
            process.Kill(entireProcessTree: true);
            var killed = await stderrTask;
            bool timedOut = timeoutCts.IsCancellationRequested;
            if (timedOut)
                _logger.LogWarning("Git process timed out after {Timeout}", options.Timeout);
            return new GitCommandResult(-1, string.Empty, killed, TimedOut: timedOut);
        }

        var stdout = await stdoutTask;
        var stderr = await stderrTask;

        _logger.LogDebug("Git exited {Code}. stdout={Stdout}", process.ExitCode, stdout.Trim());

        return new GitCommandResult(process.ExitCode, stdout, stderr, TimedOut: false);
    }
}
