namespace GitSync.Services;

public interface ISyncLoopOrchestrator
{
    Task<int> RunAsync(CancellationToken cancellationToken);
}
