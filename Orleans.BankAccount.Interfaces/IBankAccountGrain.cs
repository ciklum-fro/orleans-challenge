namespace Orleans.BankAccount.Interfaces;

public interface IBankAccountGrain : IGrainWithGuidKey
{
    [Transaction(TransactionOption.Join)]
    Task Withdraw(int amount);

    [Transaction(TransactionOption.Join)]
    Task Deposit(int amount);
    
    [Transaction(TransactionOption.CreateOrJoin)]
    Task<double> GetBalance();
    
    [Transaction(TransactionOption.CreateOrJoin)]
    Task<string> GetOwner();
}