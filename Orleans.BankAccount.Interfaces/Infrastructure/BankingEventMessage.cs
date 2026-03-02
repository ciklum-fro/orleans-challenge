namespace Orleans.BankAccount.Interfaces.Infrastructure;

/// <summary>
/// Banking event message for external event publishing.
/// </summary>
public record BankingEventMessage
{
    public string EventId { get; init; } = Guid.NewGuid().ToString();
    public string EventType { get; init; } = string.Empty;
    public Guid AccountId { get; init; }
    public decimal Amount { get; init; }
    public decimal BalanceAfter { get; init; }
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
    public Dictionary<string, string> Metadata { get; init; } = new();
}