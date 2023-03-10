using System.Diagnostics;
using App.Metrics;
using App.Metrics.Formatters.Prometheus;
using Eocron.Sharding.AppMetrics;
using Eocron.Sharding.Handlers;
using Eocron.Sharding.Jobs;
using Eocron.Sharding.Options;
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
                    new MetricsPrometheusProtobufOutputFormatter(new MetricsPrometheusOptions { NewLineFormat = NewLineFormat.Unix });
                x.MetricsTextEndpointOutputFormatter =
                    new MetricsPrometheusTextOutputFormatter(new MetricsPrometheusOptions { NewLineFormat = NewLineFormat.Unix });
            });
            services.AddSingleton<IInputOutputHandlerFactory<string, string, string>>(x=> new TestAppHandlerFactory());
            services.AddShardFactory<string, string, string>((sp, builder) =>
            {
                builder
                    .WithProcessJob(
                        new ProcessShardOptions
                        {
                            StartInfo = new ProcessStartInfo("Tools/Eocron.Sharding.TestApp.exe", "stream")
                                .ConfigureAsService()
                        })
                    .WithAppMetrics(new AppMetricsShardOptions())
                    .WithAutoRestart(TimeSpan.FromSeconds(10));
            });
            services.AddSingleton(x =>
                new ConstantShardPool<string, string, string>(
                    x.GetRequiredService<ILoggerFactory>().CreateLogger<ConstantShardPool<string, string, string>>(),
                    x.GetRequiredService<IShardFactory<string, string, string>>(),
                    new ConstantShardPoolOptions
                    {
                        PoolSize = 3
                    }));
            services.AddSingleton<IShardPool<string, string, string>>(x => x.GetRequiredService<ConstantShardPool<string, string, string>>());
            services.AddSingleton<IHostedService>(x => new JobHostedService(x.GetRequiredService<ConstantShardPool<string, string, string>>()));
            services.AddSingleton<IShardProvider<string, string, string>>(x => x.GetRequiredService<IShardPool<string, string, string>>());
        }
    }
}
