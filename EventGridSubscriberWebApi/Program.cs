using EventGridSubscriberWebApi.Options;
using EventGridSubscriberWebApi.Services;
using Serko.Messaging.EventIngestion.Extensions;

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
            builder.Services.AddEventGridIngestion<SqlEventIngestionService>(
                eventsIngestionHostedServiceOptions: opts => builder.Configuration.GetSection("EventsIngestionHostedService").Bind(opts),
                eventGridIngestionServiceOptions: opts => builder.Configuration.GetSection("EventGridIngestionService").Bind(opts),
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
