import json, os, datetime
import azure.functions as func
from azure.eventhub import EventHubProducerClient, EventData

def main(req: func.HttpRequest) -> func.HttpResponse:
    tenant_id = req.headers.get("X-Tenant-Id", "unknown")
    try:
        body = req.get_json()
    except ValueError:
        body = {}

    evt = {
        "tenantId": tenant_id,
        "inventoryId": body.get("inventoryId"),
        "item": body.get("item"),
        "quantity": body.get("quantity"),
        "timestampUtc": body.get("timestampUtc"),
        "receivedUtc": datetime.datetime.utcnow().isoformat() + "Z",
        "original": body if isinstance(body, dict) else None
    }

    payload = json.dumps(evt, separators=(",", ":")).encode("utf-8")

    try:
        cs  = os.getenv("EH__ConnectionString")
        hub = os.getenv("EH__Name", "eh-pipeline-status")
        producer = EventHubProducerClient.from_connection_string(cs, eventhub_name=hub)
        with producer:
            producer.send_batch([EventData(payload)], partition_key=tenant_id)
        return func.HttpResponse(status_code=202)
    except Exception:
        return func.HttpResponse("Failed to enqueue", status_code=500)
