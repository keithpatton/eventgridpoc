using EventGridSubscriberWebApi.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serko.Messaging.EventIngestion.Extensions;

namespace EventGridSubscriberHost
{
    internal class Program
    {
        static void Main(string[] args)
        {
            CreateHostBuilder(args).Build().Run();
        }

        public static IHostBuilder CreateHostBuilder(string[] args)
        {
            var builder = Host.CreateDefaultBuilder(args)
                 .ConfigureAppConfiguration((hostingContext, config) =>
                 {
                     var env = hostingContext.HostingEnvironment;
                     config.AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                        .AddJsonFile($"appsettings.{env.EnvironmentName}.json", optional: true, reloadOnChange: true);
                     if (env.IsDevelopment())
                     {
                         config.AddUserSecrets<Program>();
                     }
                 })
                .ConfigureServices((hostContext, services) =>
                {
                    var config = hostContext.Configuration;
                    services.AddEventGridIngestion<TestEventIngestionService>(
                        eventsIngestionHostedServiceOptions: opts => config.GetSection("EventsIngestionHostedService").Bind(opts),
                        eventGridIngestionServiceOptions: opts => config.GetSection("EventGridIngestionService").Bind(opts),
                        redisLockServiceOptions: opts => config.GetSection("RedisLockService").Bind(opts));
                });
            return builder;
        }
    }
}
