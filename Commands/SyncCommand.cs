using System.CommandLine;
using System.CommandLine.Invocation;
using GitSync.Infrastructure;
using GitSync.Models;
using GitSync.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace GitSync.Commands;

public sealed class SyncCommand : RootCommand
{
    // -------------------------------------------------------------------------
    // CLI options
    // -------------------------------------------------------------------------

    private static readonly Option<DirectoryInfo> PathOption = new(
        "--path",
        description: "Target git repository directory (defaults to current directory)",
        getDefaultValue: () => new DirectoryInfo(Directory.GetCurrentDirectory()));

    private static readonly Option<TimeSpan> IntervalOption = new(
        "--interval",
        description: "Delay between sync iterations (minimum 00:00:00.100)",
        getDefaultValue: () => TimeSpan.FromSeconds(1));

    private static readonly Option<TimeSpan?> DurationOption = new(
        "--duration",
        description: "How long to run (positive TimeSpan). Mutually exclusive with --end-time");

    private static readonly Option<DateTimeOffset?> EndTimeOption = new(
        "--end-time",
        description: "Absolute end time in ISO 8601 format. Mutually exclusive with --duration");

    private static readonly Option<string> MessageOption = new(
        "--message",
        description: "Commit message template. Use {timestamp} as a placeholder",
        getDefaultValue: () => "Auto-sync: {timestamp}");

    private static readonly Option<LogLevel> LogLevelOption = new(
        "--log-level",
        description: "Minimum log level",
        getDefaultValue: () => LogLevel.Information);

    private static readonly Option<TimeSpan> GitTimeoutOption = new(
        "--git-timeout",
        description: "Maximum time to wait for any single git command",
        getDefaultValue: () => TimeSpan.FromSeconds(30));

    // -------------------------------------------------------------------------
    // Constructor
    // -------------------------------------------------------------------------

    public SyncCommand()
        : base("Auto-pull, commit, and push a git repository on a configurable schedule")
    {
        AddOption(PathOption);
        AddOption(IntervalOption);
        AddOption(DurationOption);
        AddOption(EndTimeOption);
        AddOption(MessageOption);
        AddOption(LogLevelOption);
        AddOption(GitTimeoutOption);

        this.SetHandler(async ctx =>
        {
            ctx.ExitCode = await HandleAsync(ctx);
        });
    }

    // -------------------------------------------------------------------------
    // Handler
    // -------------------------------------------------------------------------

    private static async Task<int> HandleAsync(InvocationContext ctx)
    {
        var path       = ctx.ParseResult.GetValueForOption(PathOption)!;
        var interval   = ctx.ParseResult.GetValueForOption(IntervalOption);
        var duration   = ctx.ParseResult.GetValueForOption(DurationOption);
        var endTime    = ctx.ParseResult.GetValueForOption(EndTimeOption);
        var message    = ctx.ParseResult.GetValueForOption(MessageOption)!;
        var logLevel   = ctx.ParseResult.GetValueForOption(LogLevelOption);
        var gitTimeout = ctx.ParseResult.GetValueForOption(GitTimeoutOption);

        // Validate minimum interval
        if (interval < TimeSpan.FromMilliseconds(100))
        {
            Console.Error.WriteLine(
                $"--interval {interval} is below the minimum allowed value of 00:00:00.100");
            return 1;
        }

        // Validate --duration positivity
        if (duration.HasValue && duration.Value <= TimeSpan.Zero)
        {
            Console.Error.WriteLine("--duration must be a positive TimeSpan");
            return 1;
        }

        // Validate mutual exclusivity
        if (endTime.HasValue && duration.HasValue)
        {
            Console.Error.WriteLine("--end-time and --duration are mutually exclusive; specify only one");
            return 1;
        }

        var startTime = DateTimeOffset.UtcNow;
        var resolvedEndTime = endTime
            ?? (duration.HasValue ? startTime + duration.Value : startTime + TimeSpan.FromHours(8));

        var options = new SyncOptions(
            Path: path,
            Interval: interval,
            EndTime: resolvedEndTime,
            MessageTemplate: message,
            LogLevel: logLevel,
            GitTimeout: gitTimeout);

        // Build DI container
        var services = new ServiceCollection();
        services.AddGitSyncServices(options);
        await using var provider = services.BuildServiceProvider();

        var git = provider.GetRequiredService<IGitService>();

        // Startup validation: git executable
        var gitExe = await git.FindGitExecutableAsync();
        if (gitExe is null)
        {
            Console.Error.WriteLine(
                "git executable not found on PATH. Please install git and ensure it is on the system PATH.");
            return 1;
        }

        // Startup validation: repository
        var isRepo = await git.ValidateRepositoryAsync(path, ctx.GetCancellationToken());
        if (!isRepo)
        {
            Console.Error.WriteLine(
                $"'{path.FullName}' does not exist or is not a git repository.");
            return 1;
        }

        var orchestrator = provider.GetRequiredService<ISyncLoopOrchestrator>();
        return await orchestrator.RunAsync(ctx.GetCancellationToken());
    }
}
