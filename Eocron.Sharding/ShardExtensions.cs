using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Eocron.Sharding.Helpers;
using Eocron.Sharding.Messaging;
using Eocron.Sharding.Pools;
using Eocron.Sharding.Processing;

namespace Eocron.Sharding
{
    public static class ShardExtensions
    {
        public static async Task<IShard<TInput, TOutput, TError>> ReserveFreeAsync<TInput, TOutput, TError>(this IShardManager<TInput, TOutput, TError> shardManager, CancellationToken ct)
        {
            IShard<TInput, TOutput, TError> shard = null;
            string shardId;
            await DelayHelper.WhileTrueAsync(() => Task.FromResult(!shardManager.TryReserveFree(out shardId, out shard)), ct).ConfigureAwait(false);
            return shard;
        }

        public static async Task PublishAndHandleUntilReadyAsync<TInput, TOutput, TError>(
            this IShard<TInput, TOutput, TError> shard,
            IEnumerable<BrokerMessage<TInput>> messages,
            Func<List<BrokerMessage<TOutput>>, CancellationToken, Task> outputHandler,
            Func<List<BrokerMessage<TError>>, CancellationToken, Task> errorHandler,
            CancellationToken ct)
        {
            ClearOutputAndErrors(shard);
            await shard.PublishAsync(messages, ct).ConfigureAwait(false);
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            var consumers = Task.WhenAll(
                ConsumeAsync(shard.Outputs, outputHandler, cts.Token, ct),
                ConsumeAsync(shard.Errors, errorHandler, cts.Token, ct));
            await WhenReady(shard, cts.Token).ConfigureAwait(false);
            cts.Cancel();
            await consumers.ConfigureAwait(false);
        }

        private static async Task ConsumeAsync<T>(
            ChannelReader<T> channel, 
            Func<List<T>, CancellationToken, Task> handler, 
            CancellationToken consumeCt, 
            CancellationToken stopToken)
        {
            await Task.Yield();
            while (!consumeCt.IsCancellationRequested)
            {
                try
                {
                    await channel.WaitToReadAsync(consumeCt).ConfigureAwait(false);
                    var tmp = new List<T>();
                    while (channel.TryRead(out var item))
                    {
                        tmp.Add(item);
                    }
                    if (tmp.Count > 0)
                    {
                        await handler(tmp, stopToken).ConfigureAwait(false);
                    }
                }
                catch (OperationCanceledException) when (consumeCt.IsCancellationRequested && !stopToken.IsCancellationRequested)
                {
                    break;
                }
            }
        }

        public static void ClearOutputAndErrors<TOutput, TError>(
            this IShardOutputProvider<TOutput, TError> shard)
        {
            while (shard.Outputs.TryRead(out var _) || shard.Errors.TryRead(out var _)) { }
        }

        public static async Task WhenReady(
            this IShardStateProvider shard,
            CancellationToken ct)
        {
            await DelayHelper.WhileTrueAsync(async () => !await shard.IsReadyAsync(ct).ConfigureAwait(false), ct).ConfigureAwait(false);
        }


    }
}