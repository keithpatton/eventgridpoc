using Azure.Messaging;
using Serko.Messaging.EventIngestion.Abstractions;
using MediatR;

namespace EventGridSubscriberWebApi.Services
{

    /// <summary>
    /// Processes all events
    /// </summary>
    public class EventIngestionService : IEventIngestionService
    {
        private readonly IMediator _mediator;

        public EventIngestionService(IMediator mediator)
        {
            _mediator = mediator;
        }

        public async Task IngestAsync(CloudEvent cloudEvent)
        {
            await _mediator.Publish(new IngestEventRequest(cloudEvent));
        }
    }

    public class IngestEventRequest : IRequest
    {
        public CloudEvent CloudEvent { get; }

        public IngestEventRequest(CloudEvent cloudEvent)
        {
            CloudEvent = cloudEvent;
        }
    }

}