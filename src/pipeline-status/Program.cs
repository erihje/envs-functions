#nullable enable
using System;
using Azure.Identity;
using Azure.Messaging.EventHubs.Producer;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = FunctionsApplication.CreateBuilder(args);

// Minimal Functions hosting pipeline (.NET 8 isolated)
builder.ConfigureFunctionsWebApplication();

// Telemetry (App Insights)
builder.Services
    .AddApplicationInsightsTelemetryWorkerService()
    .ConfigureFunctionsApplicationInsights();

// Useful defaults
builder.Services.AddHttpClient();

// Event Hubs Producer Client (SAS OR Managed Identity)
// Env/config keys supported:
//   SAS:
//     EH__ConnectionString   (or EH:ConnectionString)
//     EH__Name               (or EH:Name) - only needed if CS is namespace-level (no EntityPath)
//   Managed Identity (RBAC):
//     EH__Fqdn               (or EH:Fqdn) - e.g. ehns-ns-envs-prd-swc-01.servicebus.windows.net
//     EH__Name               (or EH:Name) - e.g. eh-pipeline-status
builder.Services.AddSingleton<EventHubProducerClient>(sp =>
{
    var config = sp.GetRequiredService<IConfiguration>();

    // Nullable because IConfiguration indexer returns string?
    string? cs   = config["EH__ConnectionString"] ?? config["EH:ConnectionString"];
    var     name = config["EH__Name"]            ?? config["EH:Name"]            ?? "eh-pipeline-status";
    string? fqdn = config["EH__Fqdn"]            ?? config["EH:Fqdn"]; // namespace FQDN

    // Prefer SAS if provided (keeps your current behavior)
    if (!string.IsNullOrWhiteSpace(cs))
    {
        bool hasEntityPath = cs.IndexOf("EntityPath=", StringComparison.OrdinalIgnoreCase) >= 0;
        return hasEntityPath
            ? new EventHubProducerClient(cs)         // hub-level SAS
            : new EventHubProducerClient(cs, name);  // namespace SAS + explicit hub
    }

    // Otherwise use Managed Identity (RBAC)
    if (string.IsNullOrWhiteSpace(fqdn))
        throw new InvalidOperationException(
            "Configure either EH__ConnectionString (SAS) or EH__Fqdn (+ optional EH__Name) for Managed Identity.");

    var credential = new DefaultAzureCredential();   // uses the Function App’s managed identity in Azure
    return new EventHubProducerClient(fqdn, name, credential);
});

var app = builder.Build();
app.Run();
