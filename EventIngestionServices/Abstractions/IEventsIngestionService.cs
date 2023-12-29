namespace EventIngestionServices.Abstractions
{
    /// <summary>
    /// Defines a contract for a service responsible for ingesting a batch of events.
    /// </summary>
    public interface IEventsIngestionService
    {
        /// <summary>
        /// Asynchronously ingests a batch of events.
        /// </summary>
        /// <returns>A task that represents the asynchronous operation of ingesting events.</returns>
        /// <remarks>
        /// Implementations of this method should handle the ingestion of a batch of events. This could include retrieving events from a source like 
        /// Azure Event Grid, processing them, and potentially storing or forwarding them to another system or service.
        /// The specific implementation could vary based on the source of the events and the desired processing logic.
        /// </remarks>
        Task IngestAsync();
    }
}
