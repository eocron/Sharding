using System;
using Eocron.Sharding.Handlers;
using Eocron.Sharding.Jobs;
using Eocron.Sharding.Options;
using Eocron.Sharding.Processing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Eocron.Sharding
{
    public static class ShardBuilderExtensions
    {

        /// <summary>
        ///     Periodically restart shard.
        /// </summary>
        /// <typeparam name="TInput"></typeparam>
        /// <typeparam name="TOutput"></typeparam>
        /// <typeparam name="TError"></typeparam>
        /// <param name="builder"></param>
        /// <param name="interval"></param>
        /// <param name="forceOnBusy">
        ///     True - restarter should ignore current state of shard; False - restarter will wait until
        ///     shard is ready to serve messages (i.e not busy with anything, and will not obstruct processing)
        /// </param>
        /// <returns></returns>
        public static ShardBuilder<TInput, TOutput, TError> WithAutoRestart<TInput, TOutput, TError>(
            this ShardBuilder<TInput, TOutput, TError> builder,
            TimeSpan interval,
            bool forceOnBusy = false)
        {
            builder.Add((s, _) => s.AddSingleton<IJob>(x =>
                new RestartUntilCancelledJob(
                    new AutoRestartJob(x.GetRequiredService<ILifetimeManager>(),
                        x.GetRequiredService<IShardStateProvider>(), true, forceOnBusy),
                    x.GetRequiredService<ILoggerFactory>().CreateLogger<AutoRestartJob>(),
                    RestartPolicyOptions.Constant(interval))));
            return builder;
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
            builder.Add((s, _) =>
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
                        x.GetRequiredService<ILoggerFactory>().CreateLogger<ProcessJob<TInput, TOutput, TError>>(),
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
                .Replace<IJob, LifetimeJob>((x, prev) =>
                    new LifetimeJob(prev, x.GetRequiredService<ILoggerFactory>().CreateLogger<LifetimeJob>(), true))
                .AddSingleton<ILifetimeManager>(x => x.GetRequiredService<LifetimeJob>())
                .AddSingleton<ILifetimeProvider>(x => x.GetRequiredService<LifetimeJob>())
                .Replace<IJob>((x, prev) => new RestartUntilCancelledJob(
                    prev,
                    x.GetRequiredService<ILoggerFactory>().CreateLogger<ProcessJob<TInput, TOutput, TError>>(),
                    options.RestartPolicy));
            return container;
        }
    }
}