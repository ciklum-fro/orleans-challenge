using Orleans.BankAccount.Interfaces;
using Orleans.Concurrency;
using Orleans.Transactions.Abstractions;

namespace Orleans.BankAccount.Grains;

[GenerateSerializer]
public class AccountState
{
    [Id(0)] 
    public decimal Balance { get; set; } = 1_000m;
    
    [Id(1)]
    public string OwnerName { get; set; } = string.Empty;
}

[Reentrant]
public sealed class BankAccountGrain : Grain, IBankAccountGrain
{
    
    private readonly ITransactionalState<AccountState> _account;

    public BankAccountGrain([TransactionalState("account")] ITransactionalState<AccountState> account)
    {
        _account = account;
    }
        
    
    public Task Withdraw(decimal amount)
    {
        return _account.PerformUpdate(account =>
        {
            if (account.Balance < amount)
            {
                throw new InvalidOperationException($"Insufficient funds. Current balance: {account.Balance}, requested withdrawal: {amount}");
            }
            account.Balance -= amount;
        });
    }

    public Task Deposit(decimal amount)
    {
        if (amount <= 0)
        {
            throw new ArgumentException("Deposit amount must be positive", nameof(amount));
        }
        return _account.PerformUpdate(account => account.Balance += amount);
    }

    public Task<decimal> GetBalance()
    {
        return _account.PerformRead(account => account.Balance);
    }

    public Task<string> GetOwner()
    {
        return _account.PerformRead(account => account.OwnerName);
    }
}