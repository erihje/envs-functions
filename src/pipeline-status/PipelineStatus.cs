using System.Text.Json;
using Azure.Messaging.EventHubs;
using Azure.Messaging.EventHubs.Producer;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace Numberskills.Monitoring.PipelineStatus;

public sealed class PipelineStatus
{
    private readonly ILogger<PipelineStatus> _log;
    private readonly EventHubProducerClient _producer;

    public PipelineStatus(ILogger<PipelineStatus> log, EventHubProducerClient producer)
    {
        _log = log;
        _producer = producer;
    }

    [Function("PipelineStatus")]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "pipeline-status")] HttpRequest req)
    {
        var body = await new StreamReader(req.Body).ReadToEndAsync();
        var tenantId = req.Headers["X-Tenant-Id"].FirstOrDefault() ?? "unknown";

        JsonElement root;
        try { root = JsonDocument.Parse(body).RootElement; } catch { root = default; }

        var evt = new
        {
            tenantId,
            pipeline    = TryGet<string>(root, "pipeline"),
            runId       = TryGet<string>(root, "runId") ?? Guid.NewGuid().ToString(),
            status      = TryGet<string>(root, "status") ?? "Unknown",
            environment = TryGet<string>(root, "environment"),
            workspaceId = TryGet<string>(root, "workspaceId"),
            startedUtc  = TryGet<string>(root, "startedUtc"),
            endedUtc    = TryGet<string>(root, "endedUtc"),
            durationMs  = TryGet<long?>(root, "durationMs"),
            receivedUtc = DateTime.UtcNow,
            original    = root.ValueKind == JsonValueKind.Object ? root : default
        };

        var payload = System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(evt);

        try
        {
            using var batch = await _producer.CreateBatchAsync(new CreateBatchOptions { PartitionKey = tenantId });
            if (!batch.TryAdd(new EventData(payload)))
                return new StatusCodeResult(StatusCodes.Status413PayloadTooLarge);
            await _producer.SendAsync(batch);
            return new AcceptedResult();
        }
        catch (Exception)
        {
            return new StatusCodeResult(StatusCodes.Status500InternalServerError);
        }
    }

    private static T? TryGet<T>(JsonElement root, string name)
    {
        if (root.ValueKind != JsonValueKind.Object) return default;
        if (!root.TryGetProperty(name, out var v)) return default;
        try { return v.Deserialize<T>(); } catch { return default; }
    }
}
