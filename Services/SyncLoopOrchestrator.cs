using GitSync.Models;
using Microsoft.Extensions.Logging;

namespace GitSync.Services;

public sealed class SyncLoopOrchestrator : ISyncLoopOrchestrator
{
    private readonly SyncOptions _options;
    private readonly IGitService _git;
    private readonly ILogger<SyncLoopOrchestrator> _logger;

    private const string DefaultMessageTemplate = "Auto-sync: {timestamp}";

    public SyncLoopOrchestrator(
        SyncOptions options,
        IGitService git,
        ILogger<SyncLoopOrchestrator> logger)
    {
        _options = options;
        _git     = git;
        _logger  = logger;
    }

    public async Task<int> RunAsync(CancellationToken cancellationToken)
    {
        var endTime        = _options.EndTime;
        var iterationCount = 0;

        _logger.LogInformation(
            "GitSync starting — path={Path} interval={Interval} endTime={EndTime} logLevel={LogLevel}",
            _options.Path.FullName,
            _options.Interval,
            endTime.ToString("yyyy-MM-ddTHH:mm:ssZ"),
            _options.LogLevel);

        while (DateTimeOffset.UtcNow < endTime && !cancellationToken.IsCancellationRequested)
        {
            iterationCount++;

            try
            {
                var outcome = await ExecuteIterationAsync(iterationCount, cancellationToken);

                if (outcome == IterationOutcome.NoChanges)
                    _logger.LogDebug("Iteration {N} complete — no changes to commit", iterationCount);
                else if (outcome == IterationOutcome.Success)
                    _logger.LogInformation("Iteration {N} complete — pulled, committed, pushed", iterationCount);
                else if (outcome == IterationOutcome.Cancelled)
                    break;
                else
                    _logger.LogWarning("Iteration {N} ended with outcome: {Outcome}", iterationCount, outcome);
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Shutdown requested");
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled error in iteration {N}", iterationCount);
                // loop continues — per L2-025
            }

            try
            {
                await Task.Delay(_options.Interval, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        _logger.LogInformation(
            "GitSync session ended — {Count} iteration(s) completed", iterationCount);

        return 0;
    }

    // -------------------------------------------------------------------------

    private async Task<IterationOutcome> ExecuteIterationAsync(
        int iteration,
        CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
            return IterationOutcome.Cancelled;

        // Pull
        var pullResult = await _git.PullAsync(cancellationToken);
        if (!pullResult.Succeeded)
            return IterationOutcome.PullFailed;

        // Detect
        bool hasChanges = await _git.DetectChangesAsync(cancellationToken);
        if (!hasChanges)
            return IterationOutcome.NoChanges;

        // Stage
        var stageResult = await _git.StageAsync(cancellationToken);
        if (!stageResult.Succeeded)
            return IterationOutcome.StageFailed;

        // Commit
        var message      = ResolveMessage(_options.MessageTemplate);
        var commitResult = await _git.CommitAsync(message, cancellationToken);
        if (!commitResult.Succeeded)
            return IterationOutcome.CommitFailed;

        // Push
        var pushResult = await _git.PushAsync(cancellationToken);
        if (!pushResult.Succeeded)
            return IterationOutcome.PushFailed;

        return IterationOutcome.Success;
    }

    private string ResolveMessage(string template)
    {
        if (string.IsNullOrWhiteSpace(template))
        {
            _logger.LogWarning("Commit message template is empty; using default");
            template = DefaultMessageTemplate;
        }

        return template.Replace(
            "{timestamp}",
            DateTimeOffset.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"),
            StringComparison.Ordinal);
    }
}
