using System.Text.Json;

namespace SeniorDevAssignments.Core.Assignment2;

/// <summary>
/// Resilient webhook parser that never drops critical business fields when
/// non-essential nested nodes suffer silent schema drift.
///
/// Approach (why JsonDocument + selective deserialize):
/// 1. Parse once into a JsonDocument — zero model binding, so type changes in
///    nested nodes cannot throw during the initial parse.
/// 2. Read CustomerId / SubscriptionStatus / TotalAmount directly from the root
///    with GetProperty + typed getters. These are contractually stable.
/// 3. Attempt a *strict* deserialize of "metadata" into MetadataModel
///    (zipCode:int, notes:string). If that throws JsonException, we treat it as
///    schema drift: keep critical data, emit a warning containing the raw JSON
///    of the corrupted node.
///
/// Alternatives considered:
/// - JsonExtensionData: good for unknown properties, weak for type changes on known ones.
/// - Custom JsonConverter: heavier and still fails the whole graph if attached to the root model.
/// - JsonNode: equally valid; JsonDocument is slightly cheaper for read-only extraction.
/// </summary>
public sealed class WebhookProcessor
{
    private static readonly JsonSerializerOptions StrictOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        // Do not allow numbers-as-strings etc. — we want drift to surface on metadata.
        NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.Strict
    };

    private readonly List<string> _logs = new();

    public IReadOnlyList<string> Logs => _logs;

    public WebhookProcessResult ProcessPayload(string jsonInput)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(jsonInput);

        using var document = JsonDocument.Parse(jsonInput);
        var root = document.RootElement;

        // --- Critical fields: always extracted, never gated on metadata health ---
        var critical = new CriticalWebhookData
        {
            CustomerId = root.GetProperty("CustomerId").GetString()
                ?? throw new JsonException("CustomerId is null"),
            SubscriptionStatus = root.GetProperty("SubscriptionStatus").GetString()
                ?? throw new JsonException("SubscriptionStatus is null"),
            TotalAmount = root.GetProperty("TotalAmount").GetDecimal()
        };

        bool drift = false;
        string? warning = null;
        string? rawCorrupted = null;

        if (root.TryGetProperty("metadata", out var metadataElement))
        {
            rawCorrupted = metadataElement.GetRawText();

            try
            {
                // Strict round-trip: if zipCode is a string or notes is an array,
                // System.Text.Json throws — that is our drift signal.
                var _ = JsonSerializer.Deserialize<MetadataModel>(rawCorrupted, StrictOptions)
                    ?? throw new JsonException("metadata deserialized to null");

                // Extra structural checks: zipCode must be Number, notes must be String.
                // Deserialize alone can be permissive in edge cases; explicit type checks
                // make drift detection deterministic for the assignment samples.
                if (metadataElement.TryGetProperty("zipCode", out var zip) &&
                    zip.ValueKind != JsonValueKind.Number)
                {
                    throw new JsonException($"metadata.zipCode expected Number, got {zip.ValueKind}");
                }

                if (metadataElement.TryGetProperty("notes", out var notes) &&
                    notes.ValueKind != JsonValueKind.String &&
                    notes.ValueKind != JsonValueKind.Null)
                {
                    throw new JsonException($"metadata.notes expected String, got {notes.ValueKind}");
                }
            }
            catch (JsonException ex)
            {
                drift = true;
                warning =
                    $"[Schema Drift Warning] Non-essential 'metadata' node failed strict validation. " +
                    $"Reason: {ex.Message}. Raw corrupted node JSON: {rawCorrupted}";
                Log(warning);
            }
        }

        return new WebhookProcessResult
        {
            CriticalData = critical,
            SchemaDriftDetected = drift,
            SchemaDriftWarning = warning,
            RawCorruptedNodeJson = drift ? rawCorrupted : null
        };
    }

    private void Log(string message)
    {
        _logs.Add(message);
        Console.WriteLine(message);
    }
}
