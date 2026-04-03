namespace GitSync.Models;

public enum IterationOutcome
{
    Success,      // pulled + committed + pushed
    NoChanges,    // pulled, no local changes
    PullFailed,   // git pull returned non-zero
    StageFailed,  // git add -A returned non-zero
    CommitFailed, // git commit returned non-zero
    PushFailed,   // git push returned non-zero
    Cancelled     // CancellationToken fired during iteration
}
