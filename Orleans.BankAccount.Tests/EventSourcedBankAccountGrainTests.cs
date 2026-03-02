using Orleans.BankAccount.Interfaces;
using Orleans.TestingHost;

namespace Orleans.BankAccount.Tests;

/// <summary>
/// Tests for EventSourcedBankAccountGrain event log and transaction reconstruction.
/// Validates event sourcing capabilities: viewing event history, time travel, and auditing.
/// </summary>
public class EventSourcedBankAccountGrainTests : IAsyncLifetime
{
    private TestCluster _cluster = null!;

    /// <summary>
    /// Initialize Orleans test cluster with event sourcing support
    /// </summary>
    public async Task InitializeAsync()
    {
        var builder = new TestClusterBuilder();
        builder.AddSiloBuilderConfigurator<TestSiloConfigurator>();
        _cluster = builder.Build();
        await _cluster.DeployAsync();
    }

    /// <summary>
    /// Cleanup test cluster resources
    /// </summary>
    public async Task DisposeAsync()
    {
        await _cluster.StopAllSilosAsync();
        await _cluster.DisposeAsync();
    }

    private class TestSiloConfigurator : ISiloConfigurator
    {
        public void Configure(ISiloBuilder siloBuilder)
        {
            siloBuilder.AddMemoryGrainStorageAsDefault();
            siloBuilder.UseTransactions();
            
            // Add log consistency provider for event sourcing
            siloBuilder.AddLogStorageBasedLogConsistencyProvider("LogStorage");
            siloBuilder.AddMemoryGrainStorage("LogStorage");
        }
    }

    /// <summary>
    /// Test: GetEventLog returns empty log for new account
    /// </summary>
    [Fact(DisplayName = "GetEventLog on new account should return empty log with initial state")]
    public async Task GetEventLog_NewAccount_ReturnsEmptyLog()
    {
        // Arrange
        var accountId = Guid.NewGuid();
        var account = _cluster.GrainFactory.GetGrain<IEventSourcedBankAccountGrain>(accountId);

        // Act
        var eventLog = await account.GetEventLog();

        // Assert
        Assert.NotNull(eventLog);
        Assert.Equal(1000m, eventLog.CurrentBalance);
        Assert.Equal(1000m, eventLog.InitialBalance);
        Assert.Equal(0, eventLog.TotalEvents);
        Assert.Empty(eventLog.Events);
        Assert.Equal(0m, eventLog.TotalDeposited);
        Assert.Equal(0m, eventLog.TotalWithdrawn);
    }

    /// <summary>
    /// Test: GetEventLog shows all deposit transactions
    /// </summary>
    [Fact(DisplayName = "GetEventLog should show all deposit transactions in order")]
    public async Task GetEventLog_AfterDeposits_ShowsAllTransactions()
    {
        // Arrange
        var accountId = Guid.NewGuid();
        var account = _cluster.GrainFactory.GetGrain<IEventSourcedBankAccountGrain>(accountId);

        // Act - Perform multiple deposits
        await account.Deposit(500m);
        await account.Deposit(300m);
        await account.Deposit(200m);

        var eventLog = await account.GetEventLog();

        // Assert
        Assert.Equal(3, eventLog.TotalEvents);
        Assert.Equal(3, eventLog.Events.Count);
        Assert.Equal(2000m, eventLog.CurrentBalance); // 1000 + 500 + 300 + 200
        Assert.Equal(1000m, eventLog.TotalDeposited);
        Assert.Equal(0m, eventLog.TotalWithdrawn);

        // Verify event order and details
        Assert.Equal("Deposit", eventLog.Events[0].EventType);
        Assert.Equal(500m, eventLog.Events[0].Amount);
        Assert.Equal(1500m, eventLog.Events[0].BalanceAfterEvent);

        Assert.Equal("Deposit", eventLog.Events[1].EventType);
        Assert.Equal(300m, eventLog.Events[1].Amount);
        Assert.Equal(1800m, eventLog.Events[1].BalanceAfterEvent);

        Assert.Equal("Deposit", eventLog.Events[2].EventType);
        Assert.Equal(200m, eventLog.Events[2].Amount);
        Assert.Equal(2000m, eventLog.Events[2].BalanceAfterEvent);
    }

    /// <summary>
    /// Test: GetEventLog shows deposits and withdrawals with correct balance evolution
    /// </summary>
    [Fact(DisplayName = "GetEventLog should show mixed transactions with correct balance evolution")]
    public async Task GetEventLog_MixedTransactions_ShowsCorrectBalanceEvolution()
    {
        // Arrange
        var accountId = Guid.NewGuid();
        var account = _cluster.GrainFactory.GetGrain<IEventSourcedBankAccountGrain>(accountId);

        // Act - Perform mixed transactions
        await account.Deposit(2000m);   // Balance: 3000
        await account.Withdraw(500m);   // Balance: 2500
        await account.Deposit(1000m);   // Balance: 3500
        await account.Withdraw(300m);   // Balance: 3200

        var eventLog = await account.GetEventLog();

        // Assert
        Assert.Equal(4, eventLog.TotalEvents);
        Assert.Equal(3200m, eventLog.CurrentBalance);
        Assert.Equal(3000m, eventLog.TotalDeposited);
        Assert.Equal(800m, eventLog.TotalWithdrawn);

        // Verify balance evolution
        Assert.Equal(3000m, eventLog.Events[0].BalanceAfterEvent);
        Assert.Equal(2500m, eventLog.Events[1].BalanceAfterEvent);
        Assert.Equal(3500m, eventLog.Events[2].BalanceAfterEvent);
        Assert.Equal(3200m, eventLog.Events[3].BalanceAfterEvent);
    }

    /// <summary>
    /// Test: GetStateAtVersion returns initial state at version 0
    /// </summary>
    [Fact(DisplayName = "GetStateAtVersion(0) should return initial state")]
    public async Task GetStateAtVersion_Version0_ReturnsInitialState()
    {
        // Arrange
        var accountId = Guid.NewGuid();
        var account = _cluster.GrainFactory.GetGrain<IEventSourcedBankAccountGrain>(accountId);
        await account.Deposit(500m); // Add some events

        // Act
        var snapshot = await account.GetStateAtVersion(0);

        // Assert
        Assert.Equal(0, snapshot.Version);
        Assert.Equal(1000m, snapshot.Balance);
        Assert.Contains("Initial state", snapshot.Description);
    }

    /// <summary>
    /// Test: GetStateAtVersion returns correct state after specific event
    /// Domain Rule: Time travel should show exact state at any point in history
    /// </summary>
    [Fact(DisplayName = "GetStateAtVersion should return correct historical state")]
    public async Task GetStateAtVersion_SpecificVersion_ReturnsCorrectHistoricalState()
    {
        // Arrange
        var accountId = Guid.NewGuid();
        var account = _cluster.GrainFactory.GetGrain<IEventSourcedBankAccountGrain>(accountId);

        await account.Deposit(500m);   // Version 1: Balance = 1500
        await account.Deposit(300m);   // Version 2: Balance = 1800
        await account.Withdraw(200m);  // Version 3: Balance = 1600

        // Act & Assert - Check state at each version
        var stateV1 = await account.GetStateAtVersion(1);
        Assert.Equal(1, stateV1.Version);
        Assert.Equal(1500m, stateV1.Balance);

        var stateV2 = await account.GetStateAtVersion(2);
        Assert.Equal(2, stateV2.Version);
        Assert.Equal(1800m, stateV2.Balance);

        var stateV3 = await account.GetStateAtVersion(3);
        Assert.Equal(3, stateV3.Version);
        Assert.Equal(1600m, stateV3.Balance);
    }

    /// <summary>
    /// Test: GetStateAtVersion throws for invalid version
    /// </summary>
    [Fact(DisplayName = "GetStateAtVersion should throw for version greater than current")]
    public async Task GetStateAtVersion_FutureVersion_ThrowsArgumentException()
    {
        // Arrange
        var accountId = Guid.NewGuid();
        var account = _cluster.GrainFactory.GetGrain<IEventSourcedBankAccountGrain>(accountId);
        await account.Deposit(500m); // Version 1

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(
            async () => await account.GetStateAtVersion(10)
        );
    }

    /// <summary>
    /// Test: ReconstructTransactionHistory shows complete balance evolution
    /// </summary>
    [Fact(DisplayName = "ReconstructTransactionHistory should show complete balance evolution")]
    public async Task ReconstructTransactionHistory_MultipleTransactions_ShowsCompleteEvolution()
    {
        // Arrange
        var accountId = Guid.NewGuid();
        var account = _cluster.GrainFactory.GetGrain<IEventSourcedBankAccountGrain>(accountId);

        // Act - Create transaction history
        await account.Deposit(1000m);  // 1000 -> 2000
        await account.Withdraw(300m);  // 2000 -> 1700
        await account.Deposit(500m);   // 1700 -> 2200

        var history = await account.ReconstructTransactionHistory();

        // Assert - Should have initial state + 3 transactions = 4 snapshots
        Assert.Equal(4, history.Count);

        // Verify initial state
        Assert.Equal(0, history[0].Version);
        Assert.Equal(1000m, history[0].Balance);

        // Verify state evolution
        Assert.Equal(1, history[1].Version);
        Assert.Equal(2000m, history[1].Balance);
        Assert.Contains("Deposited", history[1].Description);

        Assert.Equal(2, history[2].Version);
        Assert.Equal(1700m, history[2].Balance);
        Assert.Contains("Withdrew", history[2].Description);

        Assert.Equal(3, history[3].Version);
        Assert.Equal(2200m, history[3].Balance);
        Assert.Contains("Deposited", history[3].Description);
    }

    /// <summary>
    /// Test: GetVersion returns correct event count
    /// </summary>
    [Fact(DisplayName = "GetVersion should return correct event count")]
    public async Task GetVersion_AfterTransactions_ReturnsCorrectCount()
    {
        // Arrange
        var accountId = Guid.NewGuid();
        var account = _cluster.GrainFactory.GetGrain<IEventSourcedBankAccountGrain>(accountId);

        // Act
        var initialVersion = await account.GetVersion();
        Assert.Equal(0, initialVersion);

        await account.Deposit(100m);
        var versionAfterDeposit = await account.GetVersion();
        Assert.Equal(1, versionAfterDeposit);

        await account.Withdraw(50m);
        var versionAfterWithdraw = await account.GetVersion();
        Assert.Equal(2, versionAfterWithdraw);
    }

    /// <summary>
    /// Test: Event log provides complete audit trail
    /// Domain Rule: All transactions must be auditable for compliance
    /// </summary>
    [Fact(DisplayName = "Event log should provide complete audit trail with timestamps")]
    public async Task GetEventLog_AllTransactions_ProvidesCompleteAuditTrail()
    {
        // Arrange
        var accountId = Guid.NewGuid();
        var account = _cluster.GrainFactory.GetGrain<IEventSourcedBankAccountGrain>(accountId);

        // Act - Create audit trail
        var beforeDeposit = DateTime.UtcNow;
        await account.Deposit(1000m);
        await Task.Delay(10); // Small delay to ensure different timestamps
        await account.Withdraw(200m);
        var afterWithdraw = DateTime.UtcNow;

        var eventLog = await account.GetEventLog();

        // Assert - Verify audit trail
        Assert.Equal(2, eventLog.TotalEvents);
        
        // All events should have timestamps
        foreach (var evt in eventLog.Events)
        {
            Assert.True(evt.Timestamp >= beforeDeposit);
            Assert.True(evt.Timestamp <= afterWithdraw);
            Assert.NotEmpty(evt.Description);
            Assert.True(evt.Version > 0);
        }
    }

    /// <summary>
    /// Test: Reconstruct history for compliance reporting
    /// </summary>
    [Fact(DisplayName = "Transaction history reconstruction supports compliance reporting")]
    public async Task ReconstructTransactionHistory_ForCompliance_ShowsAllStateChanges()
    {
        // Arrange
        var accountId = Guid.NewGuid();
        var account = _cluster.GrainFactory.GetGrain<IEventSourcedBankAccountGrain>(accountId);

        // Act - Simulate month of transactions
        await account.Deposit(5000m);
        await account.Withdraw(1500m);
        await account.Deposit(2000m);
        await account.Withdraw(500m);

        var history = await account.ReconstructTransactionHistory();

        // Assert - Verify complete history for compliance
        Assert.Equal(5, history.Count); // Initial + 4 transactions

        // Verify each state has required compliance information
        foreach (var snapshot in history)
        {
            Assert.True(snapshot.Version >= 0);
            Assert.True(snapshot.Balance >= 0);
            Assert.NotEmpty(snapshot.Description);
            Assert.NotEqual(default(DateTime), snapshot.Timestamp);
        }

        // Verify balance integrity (final state matches current balance)
        var currentBalance = await account.GetBalance();
        Assert.Equal(currentBalance, history.Last().Balance);
    }
}

