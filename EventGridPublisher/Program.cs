using Azure.Messaging;
using Azure;
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

            var namespaceEndpoint = config["NamespaceEndpoint"];
            var topicName = config["TopicName"];
            var eventType = config["EventType"];
            var topicKey = config["EventGridTopicKey"]; // user secrets

            // Construct the client using an Endpoint for a namespace as well as the access key
            // NOTE: You don't need the topicKey if using managed identity based connection
            var client = new EventGridClient(new Uri(namespaceEndpoint), new AzureKeyCredential(topicKey));

            // Publish a single CloudEvent using a custom TestModel for the event data.
            var @ev = new CloudEvent("employee_source", eventType, new TestModel { Name = "Bob", Age = 18 });
            await client.PublishCloudEventAsync(topicName, ev);

            // Publish a batch of CloudEvents.
            await client.PublishCloudEventsAsync(
                topicName,
                new[] {
                    new CloudEvent("employee_source", eventType, new TestModel { Name = "Tom", Age = 55 }),
                    new CloudEvent("employee_source", eventType, new TestModel { Name = "Alice", Age = 25 })});

            Console.WriteLine("Three events have been published to the topic. Press any key to end the application.");
            Console.ReadKey();
        }
    }

    public class TestModel
    {
        public string Name { get; set; }
        public int Age { get; set; }
    }
}
