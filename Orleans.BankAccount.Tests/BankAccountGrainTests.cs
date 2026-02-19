using Orleans.BankAccount.Interfaces;
using Orleans.TestingHost;
using Orleans.Transactions;

namespace Orleans.BankAccount.Tests;

/// <summary>
/// Tests for BankAccountGrain aggregate following DDD principles.
/// Validates business rules: deposits, withdrawals, and balance constraints.
/// </summary>
public class BankAccountGrainTests : IAsyncLifetime
{
    private TestCluster _cluster = null!;

    /// <summary>
    /// Initialize Orleans test cluster with transactional state support
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
        }
    }

    /// <summary>
    /// Test: Deposit $2000 and verify the balance
    /// Domain Rule: Deposits must increase the account balance correctly
    /// </summary>
    [Fact(DisplayName = "Deposit $2000 should increase balance from $1000 to $3000")]
    public async Task Deposit_ValidAmount_IncreasesBalance()
    {
        // Arrange - Setup aggregate with initial balance of $1000
        var accountId = Guid.NewGuid();
        var account = _cluster.GrainFactory.GetGrain<IBankAccountGrain>(accountId);
        var initialBalance = await account.GetBalance();
        Assert.Equal(1000m, initialBalance);

        // Act - Execute deposit of $2000
        await account.Deposit(2000m);

        // Assert - Verify balance increased to $3000
        var finalBalance = await account.GetBalance();
        Assert.Equal(3000m, finalBalance);
    }

    /// <summary>
    /// Test: Withdraw $500 when balance is $2000 → Should succeed
    /// Domain Rule: Withdrawals are allowed when sufficient funds exist
    /// </summary>
    [Fact(DisplayName = "Withdraw $500 with $2000 balance should succeed and leave $1500")]
    public async Task Withdraw_SufficientFunds_DecreasesBalance()
    {
        // Arrange - Setup account with $2000 balance
        var accountId = Guid.NewGuid();
        var account = _cluster.GrainFactory.GetGrain<IBankAccountGrain>(accountId);
        
        // Deposit $1000 to reach $2000 total (initial balance is $1000)
        await account.Deposit(1000m);
        var balanceBeforeWithdraw = await account.GetBalance();
        Assert.Equal(2000m, balanceBeforeWithdraw);

        // Act - Execute withdrawal of $500
        await account.Withdraw(500m);

        // Assert - Verify balance decreased to $1500
        var finalBalance = await account.GetBalance();
        Assert.Equal(1500m, finalBalance);
    }

    /// <summary>
    /// Test: Withdraw $3000 when balance is $2000 → Should fail
    /// Domain Rule: Withdrawals must be rejected when insufficient funds exist
    /// This enforces the aggregate's invariant that balance cannot be negative
    /// </summary>
    [Fact(DisplayName = "Withdraw $3000 with $2000 balance should throw InvalidOperationException")]
    public async Task Withdraw_InsufficientFunds_ThrowsInvalidOperationException()
    {
        // Arrange - Setup account with $2000 balance
        var accountId = Guid.NewGuid();
        var account = _cluster.GrainFactory.GetGrain<IBankAccountGrain>(accountId);
        
        // Deposit $1000 to reach $2000 total (initial balance is $1000)
        await account.Deposit(1000m);
        var balanceBeforeWithdraw = await account.GetBalance();
        Assert.Equal(2000m, balanceBeforeWithdraw);

        // Act & Assert - Verify withdrawal of $3000 throws exception
        var exception = await Assert.ThrowsAsync<OrleansTransactionAbortedException>(
            async () => await account.Withdraw(3000m)
        );
        
        // Verify the inner exception is InvalidOperationException with business context
        Assert.IsType<InvalidOperationException>(exception.InnerException);
        var innerException = (InvalidOperationException)exception.InnerException!;
        Assert.Contains("Insufficient funds", innerException.Message);
        Assert.Contains("2000", innerException.Message);
        Assert.Contains("3000", innerException.Message);

        // Verify balance remains unchanged at $2000
        var finalBalance = await account.GetBalance();
        Assert.Equal(2000m, finalBalance);
    }

    /// <summary>
    /// Additional Test: Verify multiple deposits accumulate correctly
    /// Domain Rule: Multiple deposits should compound correctly maintaining precision
    /// </summary>
    [Fact(DisplayName = "Multiple deposits should accumulate correctly with decimal precision")]
    public async Task Deposit_MultipleTransactions_AccumulatesCorrectly()
    {
        // Arrange
        var accountId = Guid.NewGuid();
        var account = _cluster.GrainFactory.GetGrain<IBankAccountGrain>(accountId);

        // Act - Perform multiple deposits
        await account.Deposit(100.50m);
        await account.Deposit(200.75m);
        await account.Deposit(300.25m);

        // Assert - Verify total: $1000 (initial) + $601.50 = $1601.50
        var finalBalance = await account.GetBalance();
        Assert.Equal(1601.50m, finalBalance);
    }

    /// <summary>
    /// Additional Test: Deposit negative amount should fail
    /// Domain Rule: Deposits must be positive values
    /// </summary>
    [Fact(DisplayName = "Deposit negative amount should throw ArgumentException")]
    public async Task Deposit_NegativeAmount_ThrowsArgumentException()
    {
        // Arrange
        var accountId = Guid.NewGuid();
        var account = _cluster.GrainFactory.GetGrain<IBankAccountGrain>(accountId);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<OrleansTransactionAbortedException>(
            async () => await account.Deposit(-100m)
        );
        
        // Verify the inner exception is ArgumentException
        Assert.IsType<ArgumentException>(exception.InnerException);
        var innerException = (ArgumentException)exception.InnerException!;
        Assert.Contains("must be positive", innerException.Message);
    }

    /// <summary>
    /// Additional Test: Withdraw exact balance amount should succeed
    /// Domain Rule: Withdrawing entire balance is a valid operation
    /// </summary>
    [Fact(DisplayName = "Withdraw exact balance amount should succeed and leave zero balance")]
    public async Task Withdraw_ExactBalance_LeavesZeroBalance()
    {
        // Arrange
        var accountId = Guid.NewGuid();
        var account = _cluster.GrainFactory.GetGrain<IBankAccountGrain>(accountId);
        var currentBalance = await account.GetBalance();

        // Act - Withdraw entire balance
        await account.Withdraw(currentBalance);

        // Assert - Balance should be zero
        var finalBalance = await account.GetBalance();
        Assert.Equal(0m, finalBalance);
    }
}