namespace Orleans.BankAccount.Interfaces;

public interface IBankAccountGrain : IGrainWithGuidKey
{
    [Transaction(TransactionOption.CreateOrJoin)]
    Task Withdraw(decimal amount);

    [Transaction(TransactionOption.CreateOrJoin)]
    Task Deposit(decimal amount);
    
    [Transaction(TransactionOption.CreateOrJoin)]
    Task<decimal> GetBalance();
    
    [Transaction(TransactionOption.CreateOrJoin)]
    Task<string> GetOwner();
}