using Eocron.Sharding.Jobs;
using Eocron.Sharding.Processing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using Eocron.Sharding.Handlers;
using Eocron.Sharding.Options;

namespace Eocron.Sharding
{
    public static class ShardBuilderExtensions
    {
        public static IServiceCollection AddShardProcessWatcherHostedService(this IServiceCollection services)
        {
            services.AddSingleton<ChildProcessWatcher>(x => new ChildProcessWatcher(x.GetRequiredService<ILogger<ChildProcessWatcher>>()));
            services.AddSingleton<IChildProcessWatcher>(x => x.GetRequiredService<ChildProcessWatcher>());
            services.AddSingleton<IHostedService>(x => new JobHostedService(x.GetRequiredService<ChildProcessWatcher>()));
            return services;
        }

        public static IServiceCollection AddShardFactory<TInput, TOutput, TError>(this IServiceCollection services,
            Action<IServiceProvider, ShardBuilder<TInput, TOutput, TError>> builderConfigurator)
        {
            services.AddSingleton(x =>
            {
                var builder = new ShardBuilder<TInput, TOutput, TError>()
                    .WithTransient(id => x.GetRequiredService<ILoggerFactory>().CreateLogger("Shard[" + id + "]"))
                    .WithTransient(x.GetRequiredService<IChildProcessWatcher>())
                    .WithTransient(
                        x.GetRequiredService<IInputOutputHandlerFactory<TInput, TOutput, TError>>());
                    builderConfigurator(x, builder);
                return builder.CreateFactory();
            });
            return services;
        }

        public static ShardBuilder<TInput, TOutput, TError> WithProcessJob<TInput, TOutput, TError>(
            this ShardBuilder<TInput, TOutput, TError> builder,
            ProcessShardOptions options)
        {
            builder.Add((s, shardId) => AddCoreDependencies<TInput, TOutput, TError>(s, shardId, options));
            return builder;
        }

        public static ShardBuilder<TInput, TOutput, TError> WithProcessJobWrap<TInput, TOutput, TError>(
            this ShardBuilder<TInput, TOutput, TError> builder,
            Func<IShardProcessJob<TInput, TOutput, TError>, IShardProcessJob<TInput, TOutput, TError>> wrapProvider)
        {
            builder.Add((s, shardId) =>
            {
                s.Replace<IShardProcessJob<TInput, TOutput, TError>>((_, prev) => wrapProvider(prev));
            });
            return builder;
        }

        private static IServiceCollection AddCoreDependencies<TInput, TOutput, TError>(
            IServiceCollection container,
            string shardId,
            ProcessShardOptions options)
        {
            container
                .AddSingleton<IShardProcessJob<TInput, TOutput, TError>>(x => 
                    new ProcessJob<TInput, TOutput, TError>(
                    options,
                    x.GetRequiredService<IInputOutputHandlerFactory<TInput, TOutput, TError>>(),
                    x.GetRequiredService<ILogger>(),
                    x.GetRequiredService<IChildProcessWatcher>(),
                    shardId))
                .AddSingleton<IImmutableShardProcess>(x =>
                    x.GetRequiredService<IShardProcessJob<TInput, TOutput, TError>>())
                .AddSingleton<IProcessDiagnosticInfoProvider>(x =>
                    x.GetRequiredService<IShardProcessJob<TInput, TOutput, TError>>())
                .AddSingleton<IShardInputManager<TInput>>(x =>
                    x.GetRequiredService<IShardProcessJob<TInput, TOutput, TError>>())
                .AddSingleton<IShardOutputProvider<TOutput, TError>>(x =>
                    x.GetRequiredService<IShardProcessJob<TInput, TOutput, TError>>())
                .AddSingleton<IShardStateProvider>(x =>
                    x.GetRequiredService<IShardProcessJob<TInput, TOutput, TError>>())
                .AddSingleton<IJob>(x =>
                    x.GetRequiredService<IShardProcessJob<TInput, TOutput, TError>>())
                .Replace<IJob, LifetimeJob>((x, prev) => new LifetimeJob(prev, x.GetRequiredService<ILogger>(), true))
                .AddSingleton<ILifetimeManager>(x => x.GetRequiredService<LifetimeJob>())
                .AddSingleton<ILifetimeProvider>(x => x.GetRequiredService<LifetimeJob>())
                .Replace<IJob>((x, prev) => new RestartUntilCancelledJob(
                    prev,
                    x.GetRequiredService<ILogger>(),
                    options.RestartPolicy));
            return container;
        }
    }
}