namespace Orleans.BankAccount.Interfaces.Infrastructure;

/// <summary>
/// Null object pattern implementation for when event publishing is disabled.
/// Useful for testing or when external event publishing is not needed.
/// </summary>
public class NullEventPublisher : IEventPublisher
{
    public Task PublishEventAsync(BankingEventMessage eventMessage) => Task.CompletedTask;
}