namespace Orleans.BankAccount.Interfaces.Exceptions;

public class AccountException(string message) : InvalidOperationException(message);