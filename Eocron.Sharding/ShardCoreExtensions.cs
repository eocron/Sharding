using Eocron.Sharding.Jobs;
using Eocron.Sharding.Processing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using Eocron.Sharding.Configuration;

namespace Eocron.Sharding
{
    public static class ShardCoreExtensions
    {
        public static IServiceCollection AddShardProcessWatcherHostedService(this IServiceCollection services)
        {
            services.AddSingleton<ChildProcessWatcher>(x => new ChildProcessWatcher(x.GetRequiredService<ILogger<ChildProcessWatcher>>()));
            services.AddSingleton<IChildProcessWatcher>(x => x.GetRequiredService<ChildProcessWatcher>());
            services.AddSingleton<IHostedService>(x => new JobHostedService(x.GetRequiredService<ChildProcessWatcher>()));
            return services;
        }

        public static ShardBuilder<TInput, TOutput, TError> WithSerializers<TInput, TOutput, TError>(
            this ShardBuilder<TInput, TOutput, TError> builder,
            IStreamWriterSerializer<TInput> inputSerializer,
            IStreamReaderDeserializer<TOutput> outputDeserializer,
            IStreamReaderDeserializer<TError> errorDeserializer)
        {
            builder.InputSerializer = inputSerializer;
            builder.OutputDeserializer = outputDeserializer;
            builder.ErrorDeserializer = errorDeserializer;
            return builder;
        }

        public static ShardBuilder<TInput, TOutput, TError> WithProcessJob<TInput, TOutput, TError>(
            this ShardBuilder<TInput, TOutput, TError> builder,
            ProcessShardOptions options)
        {
            builder.Add((s, shardId) => AddCoreDependencies(s, shardId, options, builder));
            return builder;
        }

        public static ShardBuilder<TInput, TOutput, TError> WithProcessJobWrap<TInput, TOutput, TError>(
            this ShardBuilder<TInput, TOutput, TError> builder,
            Func<IShardProcess<TInput, TOutput, TError>, IShardProcess<TInput, TOutput, TError>> wrapProvider)
        {
            builder.Add((s, shardId) => { s.Replace<IShardProcess<TInput, TOutput, TError>>((_, prev) =>
                {
                    return wrapProvider(prev);
                });
            });
            return builder;
        }

        private static IServiceCollection AddCoreDependencies<TInput, TOutput, TError>(
            IServiceCollection container,
            string shardId,
            ProcessShardOptions options,
            ShardBuilder<TInput, TOutput, TError> builder)
        {
            container
                .AddSingleton<IShardProcess<TInput, TOutput, TError>>(x => 
                    new ProcessJob<TInput, TOutput, TError>(
                    options,
                    builder.OutputDeserializer,
                    builder.ErrorDeserializer,
                    builder.InputSerializer,
                    x.GetRequiredService<ILogger>(),
                    x.GetService<IProcessStateProvider>(),
                    x.GetRequiredService<IChildProcessWatcher>(),
                    shardId))
                .AddSingleton<IImmutableShardProcess>(x =>
                    x.GetRequiredService<IShardProcess<TInput, TOutput, TError>>())
                .AddSingleton<IProcessDiagnosticInfoProvider>(x =>
                    x.GetRequiredService<IShardProcess<TInput, TOutput, TError>>())
                .AddSingleton<IShardInputManager<TInput>>(x =>
                    x.GetRequiredService<IShardProcess<TInput, TOutput, TError>>())
                .AddSingleton<IShardOutputProvider<TOutput, TError>>(x =>
                    x.GetRequiredService<IShardProcess<TInput, TOutput, TError>>())
                .AddSingleton<IShardStateProvider>(x =>
                    x.GetRequiredService<IShardProcess<TInput, TOutput, TError>>())
                .AddSingleton<IJob>(x =>
                    x.GetRequiredService<IShardProcess<TInput, TOutput, TError>>())
                .Replace<IJob, LifetimeJob>((x, prev) => new LifetimeJob(prev, x.GetRequiredService<ILogger>(), true))
                .AddSingleton<ILifetimeManager>(x => x.GetRequiredService<LifetimeJob>())
                .AddSingleton<ILifetimeProvider>(x => x.GetRequiredService<LifetimeJob>())
                .Replace<IJob>((x, prev) => new RestartUntilCancelledJob(
                    prev,
                    x.GetRequiredService<ILogger>(),
                    options.ErrorRestartInterval,
                    options.SuccessRestartInterval));
            return container;
        }
    }
}