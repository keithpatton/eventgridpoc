
using EventGridSubscriberWebApi.Abstractions;
using EventGridSubscriberWebApi.Services;

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
            builder.Services.AddSingleton<IEventsIngestionService,EventsIngestionService>();
            builder.Services.AddSingleton<IEventIngestionService,SqlEventIngestionService>();
            builder.Services.AddHostedService<EventsIngestionHostedService>();

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
