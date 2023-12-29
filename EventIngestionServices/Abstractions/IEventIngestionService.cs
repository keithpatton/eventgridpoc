using Azure.Messaging;

namespace EventIngestionServices.Abstractions
{
    /// <summary>
    /// Defines a contract for a service that ingests CloudEvent instances.
    /// </summary>
    public interface IEventIngestionService
    {
        /// <summary>
        /// Asynchronously ingests a single CloudEvent.
        /// </summary>
        /// <param name="cloudEvent">The CloudEvent instance to be ingested.</param>
        /// <returns>A task that represents the asynchronous operation of ingesting the event.</returns>
        /// <remarks>
        /// Implementations of this method should handle the processing of the provided CloudEvent.
        /// This could involve various operations such as storing the event data, processing it for business logic,
        /// or forwarding it to another system or service.
        /// </remarks>
        Task IngestAsync(CloudEvent cloudEvent);
    }
}
