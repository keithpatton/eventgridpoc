using EventGridSubscriberWebApi.Options;
using Serko.Messaging.EventPublishing.Extensions;
using Serko.Messaging.EventPublishing.Services;

namespace EventGridPublisherWebApi
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


            // Add event grid publishing 

            // SQL QUEUE (QUEUE RESILIENCE)
            //builder.Services.Configure<SqlEventQueueServiceOptions>(builder.Configuration.GetSection("SqlEventQueueService"));
            //builder.Services.AddEventGridPublishing<SqlEventQueueService>(
            //    eventGridPublishingServiceOptions: opts => builder.Configuration.GetSection("EventGridPublishingService").Bind(opts)
            //);

            // MEMORY QUEUE
            builder.Services.AddEventGridPublishing<MemoryEventQueueService>(
                eventGridPublishingServiceOptions: opts => builder.Configuration.GetSection("EventGridPublishingService").Bind(opts)
            );

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
