using EventGridPublisherWebApi.Options;
using JasperFx.Core;
using Microsoft.Extensions.Hosting;
using Oakton.Resources;
using Wolverine;
using Wolverine.EntityFrameworkCore;
using Wolverine.ErrorHandling;
using Wolverine.SqlServer;

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

            // *** Wolverine Setup Start ***
            builder.Host.UseWolverine((context, opts) =>
            {
                var connectionString = context.Configuration.GetConnectionString("SqlServer");
                opts.PersistMessagesWithSqlServer(connectionString, schema: "EventGridPublisher");
                opts.UseEntityFrameworkCoreTransactions();
                opts.Policies.UseDurableLocalQueues();
            });
            builder.Host.UseResourceSetupOnStartup();

            // Just do this if you don't want/need SQL Persistence
            //builder.Host.UseWolverine();

            // *** Wolverine Setup End ***

            // Add event grid publishing options
            builder.Services.Configure<EventGridPublishingOptions>(builder.Configuration.GetSection("EventGridPublishing"));


            var app = builder.Build();

            // Configure the HTTP request pipeline.
            if (app.Environment.IsDevelopment())
            {
                app.UseSwagger();
                app.UseSwaggerUI();
            }

            app.UseAuthorization();

            app.MapControllers();

            app.Run();
        }
    }
}