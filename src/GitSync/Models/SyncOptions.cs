using Microsoft.Extensions.Logging;

namespace GitSync.Models;

public sealed record SyncOptions(
    DirectoryInfo Path,
    TimeSpan Interval,
    DateTimeOffset EndTime,
    string MessageTemplate,
    LogLevel LogLevel,
    TimeSpan GitTimeout);
