using GitSync.Models;
using GitSync.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace GitSync.Infrastructure;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddGitSyncServices(
        this IServiceCollection services,
        SyncOptions options)
    {
        services.AddSingleton(options);
        services.AddSingleton<IGitProcessRunner, GitProcessRunner>();
        services.AddSingleton<IGitService, GitService>();
        services.AddSingleton<ISyncLoopOrchestrator, SyncLoopOrchestrator>();

        services.AddLogging(builder =>
        {
            builder.SetMinimumLevel(options.LogLevel);
            builder.AddConsole();
        });

        return services;
    }
}
