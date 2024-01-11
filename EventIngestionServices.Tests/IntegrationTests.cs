using Azure;
using Azure.Messaging;
using Azure.Messaging.EventGrid.Namespaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serko.Messaging.EventIngestion;
using Serko.Messaging.EventIngestion.Abstractions;
using Serko.Messaging.EventIngestion.Extensions;
using Serko.Messaging.EventIngestion.Options;

namespace EventIngestionServices.Tests
{
    [TestClass]
    public class IntegrationTests
    {
        private ServiceProvider _serviceProvider;
        private EventGridIngestionServiceOptions _eventGridIngestionServiceOptions; 

        [TestInitialize]
        public void Initialize()
        {
            // Create a new ServiceCollection
            var serviceCollection = new ServiceCollection();

            // Add logging
            serviceCollection.AddLogging(builder =>
            {
                builder.AddDebug();
                builder.SetMinimumLevel(LogLevel.Information);
            });

            // Build configuration
            var configuration = new ConfigurationBuilder()
                .AddJsonFile("appsettings.json")
                .AddUserSecrets<IntegrationTests>()
                .Build();

            _eventGridIngestionServiceOptions = configuration.GetSection("EventGridIngestionService").Get<EventGridIngestionServiceOptions>();

            // Register services
            serviceCollection.AddEventGridIngestion<TestEventIngestionService>(
                eventsIngestionHostedServiceOptions: opts => configuration.GetSection("EventsIngestionHostedService").Bind(opts),
                eventGridIngestionServiceOptions: opts => configuration.GetSection("EventGridIngestionService").Bind(opts),
                redisLockServiceOptions: opts => configuration.GetSection("RedisLockService").Bind(opts));

            // Build the ServiceProvider
            _serviceProvider = serviceCollection.BuildServiceProvider();
        }

        [TestMethod]
        public async Task PublishAndIngestSingleEvent()
        {
            await PublishAndAssertEventIngestion(1);
        }

        [TestMethod]
        public async Task PublishAndIngestMultipleEvents()
        {
            await PublishAndAssertEventIngestion(5);
        }

        [TestMethod]
        public async Task PublishAndIngestMultipleBatchesOfEvents()
        {
            await PublishAndAssertEventIngestion(30);
        }

        private async Task PublishAndAssertEventIngestion(int batchSize)
        {
            TestEventIngestionService.SetExpectedEventCount(batchSize);

            var hostedService = _serviceProvider.GetService<IHostedService>() as EventsIngestionHostedService;
            await hostedService.StartAsync(CancellationToken.None);

            var namespaceEndpoint = _eventGridIngestionServiceOptions.NamespaceEndpoint;
            var topic = _eventGridIngestionServiceOptions.Topics.First();
            var eventType = "personalisation";

            await PublishTopicEventsAsync(namespaceEndpoint, topic.Name, topic.Key, eventType, batchSize);

            bool allEventsProcessed = await TestEventIngestionService.WaitForAllEventsAsync();
            Assert.IsTrue(allEventsProcessed, "Not all events were processed.");

            var ingestedEvents = TestEventIngestionService.GetIngestedEvents();
            Assert.AreEqual(batchSize, ingestedEvents.Count, "The number of ingested events does not match the expected count.");
        }

        public class TestEventIngestionService : IEventIngestionService
        {
            private static readonly List<CloudEvent> _ingestedEvents = new List<CloudEvent>();
            private static TaskCompletionSource<bool> _tcs = new TaskCompletionSource<bool>();
            private static int _expectedEventCount;
            private static int _eventCounter = 0;

            public static void SetExpectedEventCount(int expectedCount)
            {
                _expectedEventCount = expectedCount;
                _eventCounter = 0;
                _ingestedEvents.Clear();
                _tcs = new TaskCompletionSource<bool>();
            }

            public Task IngestAsync(CloudEvent cloudEvent)
            {
                lock (_ingestedEvents)
                {
                    _ingestedEvents.Add(cloudEvent);
                    _eventCounter++;

                    if (_eventCounter >= _expectedEventCount)
                    {
                        _tcs.TrySetResult(true);
                    }
                }

                return Task.CompletedTask;
            }

            public static Task<bool> WaitForAllEventsAsync()
            {
                return _tcs.Task; // Used to wait for all events to be processed
            }

            public static IReadOnlyList<CloudEvent> GetIngestedEvents()
            {
                return _ingestedEvents.AsReadOnly();
            }
        }

        private static async Task PublishTopicEventsAsync(string namespaceEndpoint, string topicName, string topicKey, string eventType, int batchSize)
        {
            var eventSource = topicName; // set event source to topic name (E.g. customisaiton, location etc.)

            // Construct the client using an Endpoint for a namespace as well as the access key
            // NOTE: You don't need the topicKey if using managed identity based connection
            var client = new EventGridClient(new Uri(namespaceEndpoint), new AzureKeyCredential(topicKey));

            // Prepare a batch of CloudEvents
            var events = new List<CloudEvent>();
            for (int i = 0; i < batchSize; i++)
            {
                var name = GenerateRandomName();
                var age = Random.Shared.Next(1, 101); // Age between 1 and 100
                events.Add(new CloudEvent(eventSource, eventType, new { Name = name, Age = age }));
            }

            // Publish the batch of CloudEvents
            await client.PublishCloudEventsAsync(topicName, events);

            Console.WriteLine($"{batchSize} events have been published to the topic '{topicName}'.");
        }

        // Helper method to generate a random name
        private static string GenerateRandomName()
        {
            int length = Random.Shared.Next(3, 13); // Name length between 3 and 12 characters
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz";
            return new string(Enumerable.Repeat(chars, length)
                .Select(s => s[Random.Shared.Next(s.Length)]).ToArray());
        }
    }
}