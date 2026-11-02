using System.Text.Json.Serialization;

namespace SeniorDevAssignments.Core.Assignment2;

/// <summary>
/// Critical business fields — guaranteed stable at the JSON root.
/// </summary>
public sealed class CriticalWebhookData
{
    public required string CustomerId { get; init; }
    public required string SubscriptionStatus { get; init; }
    public required decimal TotalAmount { get; init; }
}

/// <summary>
/// Strict schema for the volatile metadata node.
/// Used only for drift detection — failure here must NOT drop critical data.
/// </summary>
public sealed class MetadataModel
{
    [JsonPropertyName("zipCode")]
    public int ZipCode { get; init; }

    [JsonPropertyName("notes")]
    public string? Notes { get; init; }
}

public sealed class WebhookProcessResult
{
    public required CriticalWebhookData CriticalData { get; init; }
    public bool SchemaDriftDetected { get; init; }
    public string? SchemaDriftWarning { get; init; }
    public string? RawCorruptedNodeJson { get; init; }
}
