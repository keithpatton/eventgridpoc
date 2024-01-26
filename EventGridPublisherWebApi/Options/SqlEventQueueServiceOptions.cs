namespace EventGridPublisherWebApi.Options
{

    /// <summary>
    /// Configuration options for SqlEventQueueServiceOptions.
    /// </summary>
    public class SqlEventQueueServiceOptions
    {
        /// <summary>
        /// Gets or sets the Sql connection string.
        /// </summary>
        /// <remarks>
        /// The connection string is required to connect to the Sql server and database.
        /// </remarks>
        public string ConnectionString { get; set; } = string.Empty;

    }

}
