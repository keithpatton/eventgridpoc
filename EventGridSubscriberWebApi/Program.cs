
using EventGridIngestionServices;
using EventGridIngestionServices.Extensions;
using EventGridSubscriiberWebApi.Options;
using EventGridSubscriiberWebApi.Services;
using Microsoft.Extensions.Configuration;

namespace EventGridSubscriberWebApi
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Add services to the container.

            builder.Services.AddControllers();
            // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen();

            // Events Ingestion Services
            builder.Services.Configure<SqlEventIngestionServiceOptions>(builder.Configuration.GetSection("SqlEventIngestionService"));
            builder.Services.AddEventsIngestion<SqlEventIngestionService>(
                eventsIngestionHostedServiceOptions: opts => builder.Configuration.GetSection("EventsIngestionHostedService").Bind(opts),
                eventsIngestionOptions: opts => builder.Configuration.GetSection("EventsIngestionService").Bind(opts),
                redisLockServiceOptions: opts => builder.Configuration.GetSection("RedisLockService").Bind(opts));

            var app = builder.Build();

            // Configure the HTTP request pipeline.
            if (app.Environment.IsDevelopment())
            {
                app.UseSwagger();
                app.UseSwaggerUI();
            }

            app.UseHttpsRedirection();

            app.UseAuthorization();


            app.MapControllers();

            app.Run();
        }
    }
}
