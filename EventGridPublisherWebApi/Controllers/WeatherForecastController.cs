using Azure.Messaging;
using EventGridPublisherWebApi.Abstractions;
using EventGridPublisherWebApi.Model;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

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
        private readonly IEventQueueService _eventQueueService;

        public WeatherForecastController(ILogger<WeatherForecastController> logger, IEventQueueService eventQueueService)
        {
            _logger = logger;
            _eventQueueService = eventQueueService;
        }

        [HttpGet(Name = "GetWeatherForecast")]
        public IEnumerable<WeatherForecast> Get()
        {
            var forecast = Enumerable.Range(1, 5).Select(index => new WeatherForecast
            {
                Date = DateTime.Now.AddDays(index),
                TemperatureC = Random.Shared.Next(-20, 55),
                Summary = Summaries[Random.Shared.Next(Summaries.Length)]
            })
            .ToArray();

            // enqueue event for publishing via background service
            var topicName = "customisation";
            var cloudEvent = new CloudEvent(source: "EventGridPublisherApi", type: "WeatherForecastRetrieved", jsonSerializableData: JsonSerializer.Serialize(forecast));
            _eventQueueService.EnqueueEvent(new EventQueueItem(cloudEvent, topicName));

            return forecast;
        }
    }
}
