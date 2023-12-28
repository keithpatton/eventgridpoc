
using EventGridSubscriberWebApi.Abstractions;
using EventGridSubscriberWebApi.Options;
using EventGridSubscriberWebApi.Services;
using Microsoft.Extensions.Configuration;
using StackExchange.Redis;

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

            // Register the Events Ingestion Services
            var config = builder.Configuration;
            builder.Services.Configure<EventsIngestionHostedServiceOptions>(config.GetSection("EventsIngestionHostedService"));
            builder.Services.Configure<EventsIngestionServiceOptions>(config.GetSection("EventsIngestionService"));
            builder.Services.Configure<RedisLockServiceOptions>(config.GetSection("RedisLockService"));
            builder.Services.Configure<SqlEventIngestionServiceOptions>(config.GetSection("SqlEventIngestionService"));
            builder.Services.AddHostedService<EventsIngestionHostedService>();
            builder.Services.AddSingleton<IEventsIngestionService, EventsIngestionService>();
            builder.Services.AddSingleton<IRedisLockService, RedisLockService>();         
            builder.Services.AddSingleton<IEventIngestionService,SqlEventIngestionService>();        

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
