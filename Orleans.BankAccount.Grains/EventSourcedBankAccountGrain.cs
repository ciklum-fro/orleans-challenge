using Orleans.BankAccount.Grains.Events;
using Orleans.BankAccount.Grains.States;
using Orleans.BankAccount.Interfaces;
using Orleans.BankAccount.Interfaces.DTOs;
using Orleans.BankAccount.Interfaces.Infrastructure;
using Orleans.EventSourcing;

namespace Orleans.BankAccount.Grains;

/// <summary>
/// Event-sourced bank account grain following CQRS and Event Sourcing patterns.
/// Commands (Deposit/Withdraw) raise events that mutate state.
/// Queries (GetBalance/GetOwner) read state without raising events.
/// Provides event log viewing and transaction reconstruction capabilities.
/// Publishes events to Pulsar for external system integration.
/// </summary>
public class EventSourcedBankAccountGrain : JournaledGrain<EventSourcedAccountState>, IEventSourcedBankAccountGrain
{
    private readonly IEventPublisher? _eventPublisher;

    public EventSourcedBankAccountGrain(IEventPublisher? eventPublisher)
    {
        _eventPublisher = eventPublisher;
    }
    
    /// <summary>
    /// Command: Withdraw funds from account.
    /// Raises WithdrawTransaction event which validates and updates balance.
    /// Publishes event to Pulsar for external system integration.
    /// </summary>
    public async Task Withdraw(decimal amount)
    {
        RaiseEvent(new WithdrawTransaction
        {
            Amount = amount
        });

        await ConfirmEvents();

        await PublishToPulsarAsync("Withdrawal", amount);
    }

    /// <summary>
    /// Command: Deposit funds to account.
    /// Raises DepositTransaction event which validates and updates balance.
    /// Publishes event to Pulsar for external system integration.
    /// </summary>
    public async Task Deposit(decimal amount)
    {
        RaiseEvent(new DepositTransaction
        {
            Amount = amount
        });

        await ConfirmEvents();
        
        await PublishToPulsarAsync("Deposit", amount);
    }

    /// <summary>
    /// Query: Get current account balance.
    /// Reads from current state without raising events (CQRS pattern).
    /// </summary>
    public Task<decimal> GetBalance()
    {
        return Task.FromResult(State.Balance);
    }

    /// <summary>
    /// Query: Get account owner name.
    /// Reads from current state without raising events (CQRS pattern).
    /// </summary>
    public Task<string> GetOwner()
    {
        return Task.FromResult(State.OwnerName);
    }

    /// <summary>
    /// Get the complete event log showing all transactions.
    /// Uses the event history stored in state rather than retrieving from log storage.
    /// </summary>
    public Task<EventLogDto> GetEventLog()
    {
        // Calculate totals from event history
        decimal totalDeposited = 0m;
        decimal totalWithdrawn = 0m;
        var eventsList = new List<TransactionEventDto>();

        foreach (var storedEvent in State.EventHistory)
        {
            eventsList.Add(new TransactionEventDto
            {
                Version = storedEvent.Version,
                EventType = storedEvent.EventType,
                Amount = storedEvent.Amount,
                BalanceAfterEvent = storedEvent.BalanceAfter,
                Description = storedEvent.EventType == "Deposit" 
                    ? $"Deposited {storedEvent.Amount:C}" 
                    : $"Withdrew {storedEvent.Amount:C}",
                Timestamp = storedEvent.Timestamp
            });

            // Track totals
            if (storedEvent.EventType == "Deposit")
            {
                totalDeposited += storedEvent.Amount;
            }
            else if (storedEvent.EventType == "Withdrawal")
            {
                totalWithdrawn += storedEvent.Amount;
            }
        }

        return Task.FromResult(new EventLogDto
        {
            CurrentBalance = State.Balance,
            OwnerName = State.OwnerName,
            TotalEvents = State.EventHistory.Count,
            InitialBalance = 1000m,
            Events = eventsList,
            TotalDeposited = totalDeposited,
            TotalWithdrawn = totalWithdrawn
        });
    }

    /// <summary>
    /// Get account state at a specific version (point in time).
    /// Enables "time travel" to see historical state.
    /// </summary>
    public Task<AccountSnapshotDto> GetStateAtVersion(int targetVersion)
    {
        if (targetVersion < 0)
        {
            throw new ArgumentException("Version must be non-negative", nameof(targetVersion));
        }

        if (targetVersion > State.EventHistory.Count)
        {
            throw new ArgumentException($"Version {targetVersion} does not exist. Current version is {State.EventHistory.Count}", nameof(targetVersion));
        }

        // Version 0 is the initial state
        if (targetVersion == 0)
        {
            return Task.FromResult(new AccountSnapshotDto
            {
                Version = 0,
                Balance = 1000m,
                OwnerName = State.OwnerName,
                Timestamp = DateTime.UtcNow,
                Description = "Initial state"
            });
        }

        // Get the event at the target version
        var eventAtVersion = State.EventHistory[targetVersion - 1];

        return Task.FromResult(new AccountSnapshotDto
        {
            Version = targetVersion,
            Balance = eventAtVersion.BalanceAfter,
            OwnerName = State.OwnerName,
            Timestamp = eventAtVersion.Timestamp,
            Description = eventAtVersion.EventType == "Deposit" 
                ? $"Deposited {eventAtVersion.Amount:C}" 
                : $"Withdrew {eventAtVersion.Amount:C}"
        });
    }

    /// <summary>
    /// Get events within a specific date range.
    /// Filters the event history based on timestamps.
    /// </summary>
    public Task<List<TransactionEventDto>> GetEventsByDateRange(DateTime fromDate, DateTime toDate)
    {
        var result = State.EventHistory
            .Where(evt => evt.Timestamp >= fromDate && evt.Timestamp <= toDate)
            .Select(evt => new TransactionEventDto
            {
                Version = evt.Version,
                EventType = evt.EventType,
                Amount = evt.Amount,
                BalanceAfterEvent = evt.BalanceAfter,
                Description = evt.EventType == "Deposit" 
                    ? $"Deposited {evt.Amount:C}" 
                    : $"Withdrew {evt.Amount:C}",
                Timestamp = evt.Timestamp
            })
            .ToList();

        return Task.FromResult(result);
    }

    /// <summary>
    /// Reconstruct complete transaction history showing balance evolution.
    /// Shows state after each event was applied.
    /// </summary>
    public Task<List<AccountSnapshotDto>> ReconstructTransactionHistory()
    {
        var history = new List<AccountSnapshotDto>();

        // Add initial state
        history.Add(new AccountSnapshotDto
        {
            Version = 0,
            Balance = 1000m,
            OwnerName = State.OwnerName,
            Timestamp = DateTime.UtcNow.AddMinutes(-State.EventHistory.Count), // Approximate
            Description = "Account created with initial balance"
        });

        // Add snapshot for each event
        foreach (var evt in State.EventHistory)
        {
            history.Add(new AccountSnapshotDto
            {
                Version = evt.Version,
                Balance = evt.BalanceAfter,
                OwnerName = State.OwnerName,
                Timestamp = evt.Timestamp,
                Description = evt.EventType == "Deposit" 
                    ? $"Deposited {evt.Amount:C} (new balance: {evt.BalanceAfter:C})"
                    : $"Withdrew {evt.Amount:C} (new balance: {evt.BalanceAfter:C})"
            });
        }

        return Task.FromResult(history);
    }

    /// <summary>
    /// Get current version (total number of confirmed events).
    /// </summary>
    public Task<int> GetVersion()
    {
        return Task.FromResult(State.EventHistory.Count);
    }

    /// <summary>
    /// Publish banking event to external systems via IEventPublisher.
    /// This enables loose coupling with notification, analytics, and audit systems.
    /// </summary>
    private async Task PublishToPulsarAsync(string eventType, decimal amount)
    {
        if (_eventPublisher == null)
        {
            return;
        }

        try
        {
            var eventMessage = new BankingEventMessage
            {
                EventType = eventType,
                AccountId = this.GetPrimaryKey(),
                Amount = amount,
                BalanceAfter = State.Balance,
                Timestamp = DateTime.UtcNow,
                Metadata = new Dictionary<string, string>
                {
                    ["GrainType"] = nameof(EventSourcedBankAccountGrain),
                    ["Version"] = State.EventHistory.Count.ToString()
                }
            };

            await _eventPublisher.PublishEventAsync(eventMessage);
        }
        catch (Exception ex)
        {
            // Log error but don't fail the grain operation
            Console.WriteLine($"Failed to publish event: {ex.Message}");
        }
    }
}