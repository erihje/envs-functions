using Azure.Messaging.EventHubs.Producer;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = FunctionsApplication.CreateBuilder(args);
builder.ConfigureFunctionsWebApplication();
builder.Services
    .AddApplicationInsightsTelemetryWorkerService()
    .ConfigureFunctionsApplicationInsights();

// Uses EH__ConnectionString (namespace- or hub-level) and optional EH__Name.
builder.Services.AddSingleton(sp =>
{
    var cs  = Environment.GetEnvironmentVariable("EH__ConnectionString");
    var hub = Environment.GetEnvironmentVariable("EH__Name");
    if (string.IsNullOrWhiteSpace(cs))
        throw new InvalidOperationException("EH__ConnectionString app setting is missing.");
    var hasEntityPath = cs.IndexOf("EntityPath=", StringComparison.OrdinalIgnoreCase) >= 0;
    return hasEntityPath
        ? new EventHubProducerClient(cs)
        : new EventHubProducerClient(cs, hub ?? "eh-pipeline-status");
});

var app = builder.Build();
app.Run();
