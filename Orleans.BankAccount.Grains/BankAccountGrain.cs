using Orleans.BankAccount.Interfaces;
using Orleans.Concurrency;
using Orleans.Transactions.Abstractions;

namespace Orleans.BankAccount.Grains;

[GenerateSerializer]
public class AccountState
{
    [Id(0)] 
    public double Balance { get; set; } = 1_000;
    
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
        
    
    public Task Withdraw(int amount)
    {
        return _account.PerformUpdate(account => account.Balance -= amount);
    }

    public Task Deposit(int amount)
    {
       return _account.PerformUpdate(account => account.Balance += amount);
    }

    public Task<double> GetBalance()
    {
        return _account.PerformRead(account => account.Balance = account.Balance);
    }

    public Task<string> GetOwner()
    {
        return _account.PerformRead(account => account.OwnerName = account.OwnerName);
    }
}