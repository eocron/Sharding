using System;
using System.Linq;
using Eocron.Sharding.Handlers;
using Eocron.Sharding.Processing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Eocron.Sharding
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddShardFactory<TInput, TOutput, TError>(this IServiceCollection services,
            Action<IServiceProvider, ShardBuilder<TInput, TOutput, TError>> builderConfigurator)
        {
            if (services.All(x => x.ServiceType != typeof(ChildProcessWatcher)))
                AddShardProcessWatcherHostedService(services);

            services.AddSingleton(x =>
            {
                var builder = new ShardBuilder<TInput, TOutput, TError>()
                    .WithTransient(x.GetRequiredService<ILoggerFactory>())
                    .WithTransient(x.GetRequiredService<IChildProcessWatcher>())
                    .WithTransient(
                        x.GetRequiredService<IInputOutputHandlerFactory<TInput, TOutput, TError>>());
                builder.ParentServiceProvider = x;
                builderConfigurator(x, builder);
                return builder.CreateFactory();
            });
            return services;
        }

        private static IServiceCollection AddShardProcessWatcherHostedService(this IServiceCollection services)
        {
            services.AddSingleton(x => new ChildProcessWatcher(x.GetRequiredService<ILogger<ChildProcessWatcher>>()));
            services.AddSingleton<IChildProcessWatcher>(x => x.GetRequiredService<ChildProcessWatcher>());
            services.AddSingleton<IHostedService>(
                x => new JobHostedService(x.GetRequiredService<ChildProcessWatcher>()));
            return services;
        }

        public static IServiceCollection Replace<TInterface, TImplementation>(
            this IServiceCollection collection, Func<IServiceProvider, TInterface, TImplementation> replacer)
            where TImplementation : TInterface
        {
            var found = collection.Single(x => x.ServiceType == typeof(TInterface));
            collection.Remove(found);
            collection.Add(new ServiceDescriptor(typeof(TImplementation), sp => replacer(sp, (TInterface)found.ImplementationFactory(sp)), found.Lifetime));
            collection.Add(new ServiceDescriptor(typeof(TInterface), sp => sp.GetRequiredService<TImplementation>(), found.Lifetime));
            return collection;
        }

        public static IServiceCollection Replace<TInterface>(
            this IServiceCollection collection, Func<IServiceProvider, TInterface, TInterface> replacer)
        {
            var found = collection.Single(x => x.ServiceType == typeof(TInterface));
            collection.Remove(found);
            collection.Add(new ServiceDescriptor(found.ServiceType, sp => replacer(sp, (TInterface)found.ImplementationFactory(sp)), found.Lifetime));
            return collection;
        }
    }
}