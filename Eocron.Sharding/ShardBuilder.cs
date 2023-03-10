using System;
using Microsoft.Extensions.DependencyInjection;

namespace Eocron.Sharding
{
    public sealed class ShardBuilder<TInput, TOutput, TError>
    {
        public delegate void ConfiguratorStep(IServiceCollection shardServices, string shardId);
        public ConfiguratorStep Configurator { get; set; }
        public IServiceProvider ParentServiceProvider { get; set; }

        public void Add(ConfiguratorStep next)
        {
            if(next == null)
                return;

            var prev = Configurator;
            Configurator = (s, id) =>
            {
                prev?.Invoke(s, id);
                next(s, id);
            };
        }

        public ShardBuilder<TInput, TOutput, TError> WithTransient<TInterface, TImplementation>(TImplementation implementation)
            where TImplementation : TInterface
            where TInterface : class
        {
            return WithTransient<TInterface>(_ => implementation);
        }

        public ShardBuilder<TInput, TOutput, TError> WithTransient<TInterface>(TInterface implementation)
            where TInterface : class
        {
            return WithTransient<TInterface>(_ => implementation);
        }

        public ShardBuilder<TInput, TOutput, TError> WithTransient<TInterface>(Func<string, TInterface> implementationProvider)
            where TInterface : class
        {
            Add((s, id) => s.AddTransient<TInterface>(sp => implementationProvider(id)));
            return this;
        }

        public IShard<TInput, TOutput, TError> Build(string shardId)
        {
            var services = new ServiceCollection();
            Configurator?.Invoke(services, shardId);
            return new ShardContainerAdapter<TInput, TOutput, TError>(services.BuildServiceProvider(), shardId);
        }

        public IShardFactory<TInput, TOutput, TError> CreateFactory()
        {
            return new ShardFactory<TInput, TOutput, TError>(this);
        }
    }
}