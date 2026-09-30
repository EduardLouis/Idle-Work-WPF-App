# Cloud Telemetry & Analytics Web API Specification
**Target Version**: v0.003  
**Status**: Ready for Cloud Deployment  
**Author**: Idle-Work Engineering  

---

## 1. Executive Summary

Idle-Work includes a dual-sink logging and diagnostics engine:
1. **Local AppData Sink**: Writes daily diagnostic logs to `%LocalAppData%\IdleWork\Logs\idlework_YYYYMMDD.log` for instantaneous on-machine troubleshooting.
2. **Cloud Web API Sink**: Batches system events, application activity spans, and unhandled crash stack traces, sending them via HTTPS POST to a configurable remote Web API endpoint.

When the `CloudEndpointUrl` setting is configured in Preferences (e.g. `https://telemetry.yourcompany.com/api/v1/telemetry/events`), Idle-Work automatically dispatches pending telemetry batches without blocking the UI thread or client workflow.

---

## 2. API Endpoint Specification

### `POST /api/v1/telemetry/events`

Receives batched telemetry payloads containing activity spans, diagnostic logs, and error traces.

#### Headers
| Header | Value | Description |
|---|---|---|
| `Content-Type` | `application/json; charset=utf-8` | JSON request payload |
| `Authorization` | `Bearer <OPTIONAL_API_KEY>` | Optional shared secret token |
| `User-Agent` | `IdleWork/v0.003 (Windows)` | Client identifier |

#### Request Payload Schema
```json
{
  "machineName": "DESKTOP-ENGINEERING-01",
  "userName": "eduard.louis",
  "appVersion": "v0.003",
  "osVersion": "Microsoft Windows NT 10.0.22631.0",
  "batchTimestampUtc": "2026-09-29T12:30:00.0000000Z",
  "entries": [
    {
      "timestamp": "2026-09-29T12:28:45.1230000Z",
      "level": "Activity",
      "tag": "ActivityTracker",
      "message": "Activity Span: Revit (1420s)",
      "exception": null,
      "properties": {
        "processName": "Revit",
        "windowTitle": "Autodesk Revit 2024 - [Hospital_BIM_Model.rvt]",
        "documentName": "Hospital_BIM_Model.rvt",
        "projectName": "ROCO R1OM SPT-Z",
        "category": "BIM",
        "durationSeconds": 1420.5,
        "state": "Active"
      }
    },
    {
      "timestamp": "2026-09-29T12:29:10.5000000Z",
      "level": "Error",
      "tag": "WindowTracker",
      "message": "Win32 handle access exception",
      "exception": "System.ComponentModel.Win32Exception (5): Access is denied\r\n   at IdleWork.App.Core.Native.User32.GetWindowThreadProcessId(IntPtr hWnd, UInt32& lpdwProcessId)",
      "properties": null
    }
  ]
}
```

#### Log Levels
- `0 (Debug)`: Internal diagnostic information.
- `1 (Info)`: General system state and startup/shutdown lifecycle events.
- `2 (Warning)`: Non-critical anomalies or recovered network interruptions.
- `3 (Error)`: Unhandled exceptions, UI crashes, or background task faults.
- `4 (Activity)`: Consolidated user activity spans with process, title, document, project, and duration.

#### Responses
- `200 OK` or `204 No Content`: Batch accepted and successfully written to database.
- `400 Bad Request`: Payload validation error.
- `401 Unauthorized`: Missing or invalid Bearer token.
- `500 Internal Server Error`: Transient database failure. Client retains up to 500 records in memory and retries on subsequent flush cycle.

---

## 3. Database Ingestion Schemas

### Relational Schema (PostgreSQL / Microsoft SQL Server)

```sql
-- 1. Devices & Users Registry
CREATE TABLE IF NOT EXISTS telemetry_devices (
    device_id VARCHAR(128) PRIMARY KEY,
    machine_name VARCHAR(128) NOT NULL,
    user_name VARCHAR(128) NOT NULL,
    os_version VARCHAR(128),
    last_seen_utc TIMESTAMP WITH TIME ZONE DEFAULT NOW()
);

-- 2. Telemetry Events (Errors, Warnings, System Logs)
CREATE TABLE IF NOT EXISTS telemetry_events (
    id BIGSERIAL PRIMARY KEY,
    received_at_utc TIMESTAMP WITH TIME ZONE DEFAULT NOW(),
    event_timestamp_utc TIMESTAMP WITH TIME ZONE NOT NULL,
    machine_name VARCHAR(128) NOT NULL,
    user_name VARCHAR(128) NOT NULL,
    app_version VARCHAR(32) NOT NULL,
    log_level VARCHAR(16) NOT NULL,
    tag VARCHAR(64) NOT NULL,
    message TEXT NOT NULL,
    exception_details TEXT,
    properties_json JSONB
);

-- Index for rapid error monitoring and incident alerts
CREATE INDEX IF NOT EXISTS idx_telemetry_events_level ON telemetry_events(log_level, event_timestamp_utc DESC);
CREATE INDEX IF NOT EXISTS idx_telemetry_events_user ON telemetry_events(user_name, event_timestamp_utc DESC);

-- 3. Activity Spans (Usage tracking & analytics)
CREATE TABLE IF NOT EXISTS telemetry_activity_usage (
    id BIGSERIAL PRIMARY KEY,
    received_at_utc TIMESTAMP WITH TIME ZONE DEFAULT NOW(),
    activity_timestamp_utc TIMESTAMP WITH TIME ZONE NOT NULL,
    machine_name VARCHAR(128) NOT NULL,
    user_name VARCHAR(128) NOT NULL,
    app_version VARCHAR(32) NOT NULL,
    process_name VARCHAR(128) NOT NULL,
    window_title VARCHAR(512),
    document_name VARCHAR(256),
    project_name VARCHAR(256),
    category VARCHAR(64),
    duration_seconds NUMERIC(10, 2) NOT NULL,
    state VARCHAR(32) NOT NULL
);

-- Indexes for BI dashboards and timesheet reporting
CREATE INDEX IF NOT EXISTS idx_usage_user_time ON telemetry_activity_usage(user_name, activity_timestamp_utc DESC);
CREATE INDEX IF NOT EXISTS idx_usage_project ON telemetry_activity_usage(project_name, activity_timestamp_utc DESC);
CREATE INDEX IF NOT EXISTS idx_usage_process ON telemetry_activity_usage(process_name, duration_seconds);
```

### BigQuery / Cloud Storage Ingestion Schema (Google Cloud)

```sql
CREATE OR REPLACE TABLE `your_project.idlework_telemetry.events` (
  batch_timestamp TIMESTAMP,
  machine_name STRING,
  user_name STRING,
  app_version STRING,
  os_version STRING,
  event_timestamp TIMESTAMP,
  log_level STRING,
  tag STRING,
  message STRING,
  exception STRING,
  properties_json STRING
)
PARTITION BY DATE(event_timestamp)
CLUSTER BY user_name, log_level, tag;
```

---

## 4. Reference Cloud Implementations

### Option A: ASP.NET Core Minimal API (C#)

```csharp
// Program.cs
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Npgsql; // or Microsoft.Data.SqlClient

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

string connectionString = builder.Configuration.GetConnectionString("TelemetryDb")!;

app.MapPost("/api/v1/telemetry/events", async ([FromBody] TelemetryBatchPayload batch) =>
{
    await using var conn = new NpgsqlConnection(connectionString);
    await conn.OpenAsync();

    foreach (var entry in batch.Entries)
    {
        if (entry.Level == "Activity" && entry.Properties != null)
        {
            // Insert into usage table
            await using var cmd = new NpgsqlCommand(@"
                INSERT INTO telemetry_activity_usage (
                    activity_timestamp_utc, machine_name, user_name, app_version,
                    process_name, window_title, document_name, project_name, category,
                    duration_seconds, state
                ) VALUES ($1, $2, $3, $4, $5, $6, $7, $8, $9, $10, $11)", conn);

            cmd.Parameters.AddWithValue(entry.Timestamp);
            cmd.Parameters.AddWithValue(batch.MachineName);
            cmd.Parameters.AddWithValue(batch.UserName);
            cmd.Parameters.AddWithValue(batch.AppVersion);
            cmd.Parameters.AddWithValue(entry.Properties.GetValueOrDefault("processName")?.ToString() ?? "");
            cmd.Parameters.AddWithValue(entry.Properties.GetValueOrDefault("windowTitle")?.ToString() ?? "");
            cmd.Parameters.AddWithValue(entry.Properties.GetValueOrDefault("documentName")?.ToString() ?? "");
            cmd.Parameters.AddWithValue(entry.Properties.GetValueOrDefault("projectName")?.ToString() ?? "");
            cmd.Parameters.AddWithValue(entry.Properties.GetValueOrDefault("category")?.ToString() ?? "");
            cmd.Parameters.AddWithValue(Convert.ToDecimal(entry.Properties.GetValueOrDefault("durationSeconds") ?? 0));
            cmd.Parameters.AddWithValue(entry.Properties.GetValueOrDefault("state")?.ToString() ?? "Active");
            await cmd.ExecuteNonQueryAsync();
        }
        else
        {
            // Insert into general logs / error table
            await using var cmd = new NpgsqlCommand(@"
                INSERT INTO telemetry_events (
                    event_timestamp_utc, machine_name, user_name, app_version,
                    log_level, tag, message, exception_details, properties_json
                ) VALUES ($1, $2, $3, $4, $5, $6, $7, $8, $9::jsonb)", conn);

            cmd.Parameters.AddWithValue(entry.Timestamp);
            cmd.Parameters.AddWithValue(batch.MachineName);
            cmd.Parameters.AddWithValue(batch.UserName);
            cmd.Parameters.AddWithValue(batch.AppVersion);
            cmd.Parameters.AddWithValue(entry.Level);
            cmd.Parameters.AddWithValue(entry.Tag);
            cmd.Parameters.AddWithValue(entry.Message);
            cmd.Parameters.AddWithValue((object?)entry.Exception ?? DBNull.Value);
            cmd.Parameters.AddWithValue(entry.Properties != null ? JsonSerializer.Serialize(entry.Properties) : "{}");
            await cmd.ExecuteNonQueryAsync();
        }
    }

    return Results.Ok(new { status = "accepted", count = batch.Entries.Count });
});

app.Run();

public record TelemetryBatchPayload(
    string MachineName,
    string UserName,
    string AppVersion,
    string OsVersion,
    DateTime BatchTimestampUtc,
    List<LogEntryDto> Entries);

public record LogEntryDto(
    DateTime Timestamp,
    string Level,
    string Tag,
    string Message,
    string? Exception,
    Dictionary<string, object>? Properties);
```

### Option B: Node.js / Express (TypeScript)

```typescript
import express from 'express';
import { Pool } from 'pg';

const app = express();
app.use(express.json({ limit: '5mb' }));

const pool = new Pool({ connectionString: process.env.DATABASE_URL });

app.post('/api/v1/telemetry/events', async (req, res) => {
  const { machineName, userName, appVersion, entries } = req.body;

  if (!Array.isArray(entries)) {
    return res.status(400).json({ error: 'Missing entries array' });
  }

  const client = await pool.connect();
  try {
    await client.query('BEGIN');
    for (const item of entries) {
      if (item.level === 'Activity' && item.properties) {
        await client.query(
          `INSERT INTO telemetry_activity_usage (
             activity_timestamp_utc, machine_name, user_name, app_version,
             process_name, window_title, document_name, project_name, category,
             duration_seconds, state
           ) VALUES ($1, $2, $3, $4, $5, $6, $7, $8, $9, $10, $11)`,
          [
            item.timestamp,
            machineName,
            userName,
            appVersion,
            item.properties.processName || '',
            item.properties.windowTitle || '',
            item.properties.documentName || '',
            item.properties.projectName || '',
            item.properties.category || '',
            item.properties.durationSeconds || 0,
            item.properties.state || 'Active',
          ]
        );
      } else {
        await client.query(
          `INSERT INTO telemetry_events (
             event_timestamp_utc, machine_name, user_name, app_version,
             log_level, tag, message, exception_details, properties_json
           ) VALUES ($1, $2, $3, $4, $5, $6, $7, $8, $9)`,
          [
            item.timestamp,
            machineName,
            userName,
            appVersion,
            item.level,
            item.tag,
            item.message,
            item.exception || null,
            JSON.stringify(item.properties || {}),
          ]
        );
      }
    }
    await client.query('COMMIT');
    res.status(200).json({ status: 'ok', received: entries.length });
  } catch (err) {
    await client.query('ROLLBACK');
    res.status(500).json({ error: 'Database write error' });
  } finally {
    client.release();
  }
});

app.listen(3000, () => console.log('Telemetry Ingest server running on port 3000'));
```

---

## 5. Security & Deployment Best Practices

1. **HTTPS Enforcement**: Always deploy with TLS 1.3 to protect workstation activity titles and usernames.
2. **Reverse Proxy & Load Balancing**: Deploy behind Cloudflare, Google Cloud Load Balancer, or AWS ALB with WAF rate limiting.
3. **Containerization**: Both reference implementations can be packaged in minimal Docker containers and deployed to Google Cloud Run, AWS ECS, or Azure Container Apps with zero-maintenance autoscaling.
4. **Data Privacy**: Window titles can contain file paths; ensure access to the `telemetry_activity_usage` table is restricted via database role privileges according to company GDPR / internal governance policies.
