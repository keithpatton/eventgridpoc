namespace EventGridSubscriberWebApi.Options
{

    /// <summary>
    /// Configuration options for SqlEventIngestionService.
    /// </summary>
    public class SqlEventIngestionServiceOptions
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
