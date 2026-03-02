namespace Orleans.BankAccount.Interfaces.DTOs;

/// <summary>
/// Represents the state of an account at a specific point in time (version).
/// Used for time-travel queries and auditing.
/// </summary>
[GenerateSerializer]
public record AccountSnapshotDto
{
    /// <summary>
    /// Event version at which this snapshot was taken.
    /// </summary>
    [Id(0)]
    public int Version { get; init; }

    /// <summary>
    /// Balance at this version.
    /// </summary>
    [Id(1)]
    public decimal Balance { get; init; }

    /// <summary>
    /// Account owner name.
    /// </summary>
    [Id(2)]
    public string OwnerName { get; init; } = string.Empty;

    /// <summary>
    /// Timestamp of the last event applied to reach this state.
    /// </summary>
    [Id(3)]
    public DateTime Timestamp { get; init; }

    /// <summary>
    /// Description of what happened at this version.
    /// </summary>
    [Id(4)]
    public string Description { get; init; } = string.Empty;
}

