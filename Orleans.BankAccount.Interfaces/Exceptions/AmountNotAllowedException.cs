namespace Orleans.BankAccount.Interfaces.Exceptions;

public class AmountNotAllowedException() : ArgumentException("Deposit amount must be positive");