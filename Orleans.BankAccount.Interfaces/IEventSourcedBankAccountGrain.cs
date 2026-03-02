using Orleans.BankAccount.Interfaces.DTOs;

namespace Orleans.BankAccount.Interfaces;

/// <summary>
/// Extended interface for event-sourced bank account operations.
/// Provides event log viewing and time-travel capabilities.
/// </summary>
public interface IEventSourcedBankAccountGrain : IBankAccountGrain
{
    /// <summary>
    /// Get the complete event log showing all transactions.
    /// Useful for audit trails and compliance.
    /// </summary>
    /// <returns>Complete event log with all transactions and summary statistics.</returns>
    Task<EventLogDto> GetEventLog();

    /// <summary>
    /// Get account state at a specific version (point in time).
    /// Enables "time travel" queries to see historical state.
    /// </summary>
    /// <param name="version">Event version to retrieve (0 = initial state, 1 = after first event, etc.)</param>
    /// <returns>Account snapshot at the specified version.</returns>
    Task<AccountSnapshotDto> GetStateAtVersion(int version);

    /// <summary>
    /// Get all events that occurred within a specific time range.
    /// Useful for generating transaction reports.
    /// </summary>
    /// <param name="fromDate">Start date (inclusive).</param>
    /// <param name="toDate">End date (inclusive).</param>
    /// <returns>List of events within the specified date range.</returns>
    Task<List<TransactionEventDto>> GetEventsByDateRange(DateTime fromDate, DateTime toDate);

    /// <summary>
    /// Reconstruct the complete transaction history showing balance evolution.
    /// Shows step-by-step how the balance changed with each transaction.
    /// </summary>
    /// <returns>List of snapshots showing state after each event.</returns>
    Task<List<AccountSnapshotDto>> ReconstructTransactionHistory();

    /// <summary>
    /// Get the current version (total number of events).
    /// </summary>
    /// <returns>Current version number.</returns>
    Task<int> GetVersion();
}

