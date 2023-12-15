using Azure.Messaging.EventHubs.Producer;
using Azure.Messaging.EventHubs;
using Microsoft.Extensions.Configuration;
using System.Text.Json;

namespace EventHubsPublisher
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            var builder = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                .AddUserSecrets<Program>();

            var config = builder.Build();

            var eventType = config["EventType"]!;
            var namespaceConnString = config["NamespaceConnString"]!;

            //Endpoint=sb://eosdomaineventsbus-poc.servicebus.windows.net/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=abT5rdacu9TzwM/mLrZQs24X50Ce/w/ky+AEhJcaxSk=
            var eventBatchSize = Convert.ToInt32(config["EventBatchSize"]!);

            while (true)
            {
                Console.WriteLine("Press any key to send events to topics. Press 'Esc' to exit.");
                var key = Console.ReadKey(); // Wait for key press
                if (key.Key == ConsoleKey.Escape) // Exit condition
                {
                    break;
                }

                Console.WriteLine("\nPublishing events...");

                // publish to topic 1
                await PublishTopicEventsAsync(namespaceConnString, config["TopicName1"]!, eventType, eventBatchSize);

                // publish to topic 2
                await PublishTopicEventsAsync(namespaceConnString, config["TopicName2"]!, eventType, eventBatchSize);

                Console.WriteLine("Events published successfully.");
            }

            Console.WriteLine("Exiting application.");
        }

        private static async Task PublishTopicEventsAsync(string namespaceConnString, string topicName, string eventType, int batchSize)
        {
            // NOTE: You don't need the namespaceConnString if using managed identity based connection
            var producer = new EventHubBufferedProducerClient(namespaceConnString, topicName);

            // The failure handler is required and invoked after all allowable reliable retries were applied
            producer.SendEventBatchFailedAsync += args =>
            {
                Console.WriteLine($"Publishing failed for {args.EventBatch.Count} events.  Error: '{args.Exception.Message}'");
                return Task.CompletedTask;
            };

            // The success handler is optional.
            //producer.SendEventBatchSucceededAsync += args =>
            //{
            //    Console.WriteLine($"{args.EventBatch.Count} events were published to partition: '{args.PartitionId}.");
            //    return Task.CompletedTask;
            //};

            try
            {
                for (var index = 0; index < batchSize; ++index)
                {
                    var name = GenerateRandomName();
                    var age = Random.Shared.Next(1, 101); // Age between 1 and 100

                    var testModel = new TestModel { Name = name, Age = age };
                    var eventBodyJson = JsonSerializer.Serialize(testModel);

                    var eventData = new EventData(eventBodyJson)
                    {
                        MessageId = Guid.NewGuid().ToString(),
                        ContentType = "application/json"
                    };
                    eventData.Properties.Add("EventSource", topicName);
                    eventData.Properties.Add("EventType", eventType);

                    await producer.EnqueueEventAsync(eventData);
                }
            }
            finally
            {
                // Closing the producer will flush any enqueued events that have not been published.
                await producer.CloseAsync();
            }

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

    public class TestModel
    {
        public string Name { get; set; } = string.Empty;
        public int Age { get; set; }
    }
}
