namespace Orleans.BankAccount.Interfaces.DTOs;

/// <summary>
/// Data Transfer Object representing a single transaction event in the event log.
/// Used for viewing event history and reconstructing account state.
/// </summary>
[GenerateSerializer]
public record TransactionEventDto
{
    /// <summary>
    /// Sequential event number (version) in the event stream.
    /// </summary>
    [Id(0)]
    public int Version { get; init; }

    /// <summary>
    /// Type of event (e.g., "DepositTransaction", "WithdrawTransaction").
    /// </summary>
    [Id(1)]
    public string EventType { get; init; } = string.Empty;

    /// <summary>
    /// Amount involved in the transaction.
    /// </summary>
    [Id(2)]
    public decimal Amount { get; init; }

    /// <summary>
    /// Timestamp when the event was created.
    /// </summary>
    [Id(3)]
    public DateTime Timestamp { get; init; }

    /// <summary>
    /// Balance after this event was applied.
    /// </summary>
    [Id(4)]
    public decimal BalanceAfterEvent { get; init; }

    /// <summary>
    /// Human-readable description of the event.
    /// </summary>
    [Id(5)]
    public string Description { get; init; } = string.Empty;
}

