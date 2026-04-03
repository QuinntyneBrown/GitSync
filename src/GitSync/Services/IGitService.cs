using GitSync.Models;

namespace GitSync.Services;

public interface IGitService
{
    Task<bool> ValidateRepositoryAsync(DirectoryInfo path, CancellationToken cancellationToken = default);
    Task<string?> FindGitExecutableAsync();
    Task<GitCommandResult> PullAsync(CancellationToken cancellationToken = default);
    Task<bool> DetectChangesAsync(CancellationToken cancellationToken = default);
    Task<GitCommandResult> StageAsync(CancellationToken cancellationToken = default);
    Task<GitCommandResult> CommitAsync(string message, CancellationToken cancellationToken = default);
    Task<GitCommandResult> PushAsync(CancellationToken cancellationToken = default);
}
