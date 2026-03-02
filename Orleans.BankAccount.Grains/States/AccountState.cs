
namespace Orleans.BankAccount.Grains.States;

[GenerateSerializer]
public class AccountState
{
    [Id(0)] 
    public decimal Balance { get; set; } = 1_000m;
    
    [Id(1)]
    public string OwnerName { get; set; } = string.Empty;
    
}