namespace Orleans.BankAccount.Grains.Events;

/// <summary>
/// Event representing a deposit command.
/// Raised when funds are deposited into an account.
/// </summary>
[GenerateSerializer]
public class DepositTransaction
{
    [Id(0)]
    public decimal Amount { get; set; }
    
    [Id(1)]
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}