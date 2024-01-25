Introduction
-
*Serko.Messaging.EventPublishing* is a .NET library designed to simplify event publishing in .Net applications.

It provides a set of services and extensions that allow for easy setup, configuration, and publishing of events.

Client applications can enqueue one or more events at a time which are then published asynchronously in a background service.

This library can be added to any .NET Host, so can be added to a standard console app as well as within an Asp.Net Core Api.

Event Grid Guidelines
-

Event Grid (Namespace Topics) are the initial supported eventing backplane.

Please see The [Eos Domain Events - Event Grid Guidlines](https://serko.atlassian.net/wiki/spaces/PFI/pages/3809084510/Eos+Domain+Events+Bus+-+Event+Grid+Guidelines) provides for additional detail on Event Grid.


Installation
-
Install with NuGet.
```powershell
Install-Package Serko.Messaging.EventPublishing
```
Or via `dotnet` cli.
```bash
dotnet add package Serko.Messaging.EventPublishing
```

Prerequisites
-
- Event Grid Namespace with Namespace Topic(s) where events will be published
- Existing .Net app from which you would like publish events.

Setting up the Services
-
Use the AddEventGridPublishing extension method to set up the services in your .NET application's Program.cs:

```
    builder.Services.AddEventGridPublishing(
        eventGridPublishingServiceOptions: opts => builder.Configuration.GetSection("EventGridPublishingService").Bind(opts)
    );
```

This does the following:

- Registers an Event Queue Service to which you can enqueue event(s).
- Register a BackgroundService which will publish events in batches from the queue asynchronously in the background

Enqueuing Events
-
You can enqueue one or many events at a time, here is an example which enqueues a single cloud event for an event grid topic:

```
    var topicName = "customisation";
    var cloudEvent = new CloudEvent(source: "{EVENT_SOURCE}", type: "{EVENT_TYPE}", jsonSerializableData: {JSON_DATA});
    _eventQueueService.EnqueueEvent(new EventQueueItem(cloudEvent, {TOPIC_NAME}));
```

Configuration
-

Below is an example of how to structure configuration settings to bind to the Options required by the library services:

For {SECRET} values, secrets.json can be used locally and be replaced from Key Vault or equivalent in other environments.

```
    "EventGridPublishingService": {
    // event grid namespace host
    "NamespaceEndpoint": "https://eosdomaineventsbus-poc.australiaeast-1.eventgrid.azure.net/",
    // max number of events to publish at once per topic
    "EventBatchSize": 250, 
    // interval between publishing attempts
    "PublishingInterval": "00:00:03", 
    // list of all supported topcis
    "Topics": [
      {
        "Name": "customisation",
        "Key": "{SECRET}"
      }
    ]
  }
```

Application Security
--
It is recommended to make use of managed identity (passwordless) connection strings where possible. 

For EventGrid, in the EventGridIngestionService options, do not supply the Topic Key to make use of managed identity (otherwise you need to supply the Key).

Observability
--
TODO - [Standard .Net SDK Observailbity APIs](https://learn.microsoft.com/en-us/dotnet/core/diagnostics/observability-with-otel#net-implementation-of-opentelemetry) are used throughout for logging, metrics and tracing.