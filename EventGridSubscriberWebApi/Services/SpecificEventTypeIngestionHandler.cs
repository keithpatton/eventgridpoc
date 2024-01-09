using MediatR;

namespace EventGridSubscriberWebApi.Services
{
    public class SpecificEventTypeIngestionHandler : IRequestHandler<IngestEventRequest>
    {

        public async Task Handle(IngestEventRequest request, CancellationToken cancellationToken)
        {
            if (request.CloudEvent.Type == "SpecificEventType")
            {
                // Process event of type "SpecificEventType"
            }
            await Task.FromResult(Task.CompletedTask); 
        }
    }

}