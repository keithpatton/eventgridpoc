using Azure.Messaging;

namespace EventGridSubscriberWebApi.Abstractions
{
    public interface IEventsIngestionService
    {
        Task IngestAsync();
    }
}
