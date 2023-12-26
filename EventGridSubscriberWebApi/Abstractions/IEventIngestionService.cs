using Azure.Messaging;

namespace EventGridSubscriberWebApi.Abstractions
{
    public interface IEventIngestionService
    {
        Task IngestAsync(CloudEvent cloudEvent);
    }
}
