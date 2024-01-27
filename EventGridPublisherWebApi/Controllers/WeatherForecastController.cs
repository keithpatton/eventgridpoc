using Azure.Messaging;
using EventGridPublisherWebApi.Handlers;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using Wolverine;

namespace EventGridPublisherWebApi.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class WeatherForecastController : ControllerBase
    {
        private static readonly string[] Summaries = new[]
        {
            "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
        };

        private readonly ILogger<WeatherForecastController> _logger;
        private readonly IMessageBus _messageBus;

        public WeatherForecastController(ILogger<WeatherForecastController> logger, IMessageBus messageBus)
        {
            _logger = logger;
            _messageBus = messageBus;
        }

        [HttpGet(Name = "GetWeatherForecast")]
        public async Task<IEnumerable<WeatherForecast>> Get()
        {
            var forecast = Enumerable.Range(1, 5).Select(index => new WeatherForecast
            {
                Date = DateTime.Now.AddDays(index),
                TemperatureC = Random.Shared.Next(-20, 55),
                Summary = Summaries[Random.Shared.Next(Summaries.Length)]
            })
            .ToArray();

            // Use Wolverine's Message Bus (local queue, resiliency and persistence support)
            // BusinessEventFiredHandler handles BusinessEventFired message automatically using naming convention
            var cloudEvent = new CloudEvent(source: "EventGridPublisherApi", type: "WeatherForecastRetrieved", jsonSerializableData: JsonSerializer.Serialize(forecast));
            await _messageBus.SendAsync(new BusinessEventFired(cloudEvent, TopicName: "customisation"));

            return forecast;
        }
    }
}
