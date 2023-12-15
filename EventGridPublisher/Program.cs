using Azure;
using Azure.Messaging;
using Azure.Messaging.EventGrid.Namespaces;
using Microsoft.Extensions.Configuration;

namespace EventGridPublisher
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
            var namespaceEndpoint = config["NamespaceEndpoint"]!;
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
                await PublishTopicEventsAsync(namespaceEndpoint, config["TopicName1"]!, config["TopicKey1"]!, eventType, eventBatchSize);

                // publish to topic 2
                await PublishTopicEventsAsync(namespaceEndpoint, config["TopicName2"]!, config["TopicKey2"]!, eventType, eventBatchSize);

                Console.WriteLine("Events published successfully.");
            }

            Console.WriteLine("Exiting application.");
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
                events.Add(new CloudEvent(eventSource, eventType, new TestModel { Name = name, Age = age }));
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

    public class TestModel
    {
        public string Name { get; set; } = string.Empty;
        public int Age { get; set; }
    }
}
