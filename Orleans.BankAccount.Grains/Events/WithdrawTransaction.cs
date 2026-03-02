namespace Orleans.BankAccount.Grains.Events;

/// <summary>
/// Event representing a withdrawal command.
/// Raised when funds are withdrawn from an account.
/// </summary>
[GenerateSerializer]
public class WithdrawTransaction
{
    [Id(0)]
    public decimal Amount { get; set; }
    
    [Id(1)]
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}