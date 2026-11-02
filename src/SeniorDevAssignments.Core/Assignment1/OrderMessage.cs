namespace SeniorDevAssignments.Core.Assignment1;

/// <summary>
/// Simulated OMS event published onto the message bus.
/// </summary>
public sealed record OrderMessage(int OrderId, string Event)
{
    public override string ToString() => $"{{ OrderId: {OrderId}, Event: \"{Event}\" }}";
}
