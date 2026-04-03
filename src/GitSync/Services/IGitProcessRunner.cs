using GitSync.Models;

namespace GitSync.Services;

public interface IGitProcessRunner
{
    Task<GitCommandResult> RunAsync(
        GitCommandOptions options,
        CancellationToken cancellationToken = default);
}
