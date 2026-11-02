# Senior C# Developer Assignments

Runnable .NET 8 solution covering both take-home problems:

1. **Asynchronous Race Condition** — parallel message consumers with strict per-`OrderId` ordering  
2. **Resilient Schema Parser** — extract critical webhook fields even when nested metadata drifts

## Projects

| Project | Role |
|---------|------|
| `SeniorDevAssignments.Core` | Shared domain logic for both assignments |
| `SeniorDevAssignments.Console` | Console app (primary submission artifact) |
| `SeniorDevAssignments.Web` | Minimal API + HTML demo (Azure hosting) |

## Run locally

```bash
# Console demos (Assignment 1 then 2)
dotnet run --project src/SeniorDevAssignments.Console

# Web UI / APIs
dotnet run --project src/SeniorDevAssignments.Web
# open http://localhost:5000
```

## Assignment 1 — concurrency model

**Chosen model:** `Channel<OrderMessage>` (simulated bus) + **3 worker tasks** + `ConcurrentDictionary<int, SemaphoreSlim>` (per-order gate).

- Different `OrderId`s never share a gate → Orders 2 and 3 stay parallel with Order 1.
- Same `OrderId` serialises on one `SemaphoreSlim(1,1)` → `Cancel` cannot start until `Create` releases.
- Preferred over `OrderId % N` partitioning (hot partitions) and over a global lock (kills throughput).

**If Create fails:** the order is marked failed; the gate is still released (no deadlock). Later `Cancel`/`Update` for that id are skipped instead of calling the legacy inventory API.

## Assignment 2 — parsing approach

**Chosen model:** `JsonDocument` for critical root fields + strict `JsonSerializer.Deserialize<MetadataModel>` on the `metadata` node.

- Root parse never binds volatile nested types, so Input B cannot throw away `CustomerId` / `SubscriptionStatus` / `TotalAmount`.
- Metadata type mismatches raise `JsonException` → **Schema Drift Warning** with the raw corrupted node JSON; critical data is still returned.

## How it scales

- Assignment 1: worker count is configurable; per-order gates are created lazily and only contend within one entity stream. Horizontal scale is achieved by sharding the inbound bus by `OrderId` upstream and keeping the same in-process gate pattern per node.
- Assignment 2: single-pass `JsonDocument` parse is O(n) in payload size with no reflection over drifted subtrees beyond the metadata attempt.

## Hosting

- **GitHub:** https://github.com/DeelanLasrado/senior-csharp-assignments
- **Azure App Service (Free F1):** blocked while subscription `892bf3be-…` is `ReadOnlyDisabledSubscription`. After re-enabling billing / the free trial, run `bash deploy-azure.sh`.
- **Render (free alternative):** repo includes `Dockerfile` + `render.yaml`. In the [Render dashboard](https://dashboard.render.com) → New → Blueprint → select this repo (no credit card required on free tier).
- **Local public tunnel (temporary):** with the web project running, `cloudflared tunnel --url http://127.0.0.1:5088`.
