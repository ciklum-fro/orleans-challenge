namespace Orleans.BankAccount.Interfaces.DTOs;

/// <summary>
/// Complete event log showing all transactions and state reconstruction.
/// Provides audit trail and ability to see how balance evolved over time.
/// </summary>
[GenerateSerializer]
public record EventLogDto
{
    /// <summary>
    /// List of all transaction events in chronological order.
    /// </summary>
    [Id(0)]
    public List<TransactionEventDto> Events { get; init; } = new();

    /// <summary>
    /// Current account balance (result of applying all events).
    /// </summary>
    [Id(1)]
    public decimal CurrentBalance { get; init; }

    /// <summary>
    /// Account owner name.
    /// </summary>
    [Id(2)]
    public string OwnerName { get; init; } = string.Empty;

    /// <summary>
    /// Total number of events in the log.
    /// </summary>
    [Id(3)]
    public int TotalEvents { get; init; }

    /// <summary>
    /// Initial balance before any events.
    /// </summary>
    [Id(4)]
    public decimal InitialBalance { get; init; }

    /// <summary>
    /// Total amount deposited across all events.
    /// </summary>
    [Id(5)]
    public decimal TotalDeposited { get; init; }

    /// <summary>
    /// Total amount withdrawn across all events.
    /// </summary>
    [Id(6)]
    public decimal TotalWithdrawn { get; init; }
}
