using Orleans.BankAccount.Grains.Events;
using Orleans.BankAccount.Interfaces.Exceptions;
using Orleans.Transactions;

namespace Orleans.BankAccount.Grains.States;

/// <summary>
/// Event-sourced account state that rebuilds itself from events.
/// Contains Apply methods for each command event (Deposit/Withdraw).
/// Queries (GetBalance/GetOwner) do not have Apply methods as they don't mutate state.
/// Maintains event history for audit and reconstruction purposes.
/// </summary>
[GenerateSerializer]
public class EventSourcedAccountState
{
    [Id(0)] public decimal Balance { get; set; } = 1_000m;

    [Id(1)] public string OwnerName { get; set; } = string.Empty;

    /// <summary>
    /// Event history for audit trail and state reconstruction.
    /// Stores all transactions that have been applied to this account.
    /// </summary>
    [Id(2)] public List<StoredEvent> EventHistory { get; set; } = new();

    /// <summary>
    /// Apply DepositTransaction event: validates and increases balance.
    /// Business Rule: Deposits must be positive amounts.
    /// </summary>
    public void Apply(DepositTransaction @event)
    {
        if (@event.Amount <= 0)
        {
            throw new OrleansTransactionException("Orleans transaction error.", 
                new AmountNotAllowedException());
        }

        Balance += @event.Amount;
        
        // Store event in history for later retrieval
        EventHistory.Add(new StoredEvent
        {
            Version = EventHistory.Count + 1,
            EventType = "Deposit",
            Amount = @event.Amount,
            BalanceAfter = Balance,
            Timestamp = @event.Timestamp
        });
    }

    /// <summary>
    /// Apply WithdrawTransaction event: validates and decreases balance.
    /// Business Rule: Withdrawals cannot exceed current balance (no overdraft).
    /// </summary>
    public void Apply(WithdrawTransaction @event)
    {
        if (Balance < @event.Amount)
        {
            throw new OrleansTransactionException("Orleans transaction error.", 
                new AccountException($"Insufficient funds. Current balance: {Balance}, requested withdrawal: {@event.Amount}"));
        }

        Balance -= @event.Amount;
        
        // Store event in history for later retrieval
        EventHistory.Add(new StoredEvent
        {
            Version = EventHistory.Count + 1,
            EventType = "Withdrawal",
            Amount = @event.Amount,
            BalanceAfter = Balance,
            Timestamp = @event.Timestamp
        });
    }
}

/// <summary>
/// Represents a stored event in the event history.
/// Contains all necessary information for audit and reconstruction.
/// </summary>
[GenerateSerializer]
public class StoredEvent
{
    [Id(0)] public int Version { get; set; }
    [Id(1)] public string EventType { get; set; } = string.Empty;
    [Id(2)] public decimal Amount { get; set; }
    [Id(3)] public decimal BalanceAfter { get; set; }
    [Id(4)] public DateTime Timestamp { get; set; }
}
