using System.Diagnostics;
using App.Metrics;
using App.Metrics.Formatters.Prometheus;
using Eocron.Sharding.AppMetrics;
using Eocron.Sharding.Handlers;
using Eocron.Sharding.Pools;
using Eocron.Sharding.Processing;
using Eocron.Sharding.TestCommon;

namespace Eocron.Sharding.TestWebApp.IoC
{
    public static class ApplicationConfigurator
    {
        public static void Configure(IServiceCollection services)
        {
            services.AddControllers();
            services.AddEndpointsApiExplorer();
            services.AddSwaggerGen();
            services.AddMetrics();
            services.AddMetricsEndpoints(x =>
            {
                x.MetricsEndpointOutputFormatter =
                    new MetricsPrometheusProtobufOutputFormatter(new MetricsPrometheusOptions()
                        { NewLineFormat = NewLineFormat.Unix });
                x.MetricsTextEndpointOutputFormatter =
                    new MetricsPrometheusTextOutputFormatter(new MetricsPrometheusOptions()
                        { NewLineFormat = NewLineFormat.Unix });
            });
            services.AddShardProcessWatcherHostedService();
            services.AddSingleton<IProcessInputOutputHandlerFactory<string, string, string>>(x=> new TestAppHandlerFactory());
            services.AddSingleton(x =>
                new ShardBuilder<string, string, string>()
                    .WithTransient(id=> x.GetRequiredService<ILoggerFactory>().CreateLogger("Shard["+id+"]"))
                    .WithTransient(x.GetRequiredService<IChildProcessWatcher>())
                    .WithTransient(x.GetRequiredService<IProcessInputOutputHandlerFactory<string, string, string>>())
                    .WithProcessJob(
                        new ProcessShardOptions
                                {
                                    StartInfo = new ProcessStartInfo("Tools/Eocron.Sharding.TestApp.exe", "stream")
                                        .ConfigureAsService(),
                                    ErrorRestartInterval = TimeSpan.FromSeconds(5),
                                    SuccessRestartInterval = TimeSpan.FromSeconds(5)
                                })
                    .WithTransient(x.GetRequiredService<IMetrics>())
                    .WithAppMetrics(new AppMetricsShardOptions())
                    .CreateFactory());
            services.AddSingleton(x =>
                new ConstantShardPool<string, string, string>(
                    x.GetRequiredService<ILoggerFactory>().CreateLogger<ConstantShardPool<string, string, string>>(),
                    x.GetRequiredService<IShardFactory<string, string, string>>(),
                    3,
                    TimeSpan.FromSeconds(5),
                    TimeSpan.FromSeconds(5)));
            services.AddSingleton<IShardPool<string, string, string>>(x => x.GetRequiredService<ConstantShardPool<string, string, string>>());
            services.AddSingleton<IHostedService>(x => new JobHostedService(x.GetRequiredService<ConstantShardPool<string, string, string>>()));
            services.AddSingleton<IShardProvider<string, string, string>>(x => x.GetRequiredService<IShardPool<string, string, string>>());
        }
    }
}
