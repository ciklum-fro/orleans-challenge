namespace Orleans.BankAccount.Interfaces.Infrastructure;

/// <summary>
/// Interface for publishing domain events to external systems.
/// Decouples grains from specific messaging infrastructure (Pulsar, Kafka, RabbitMQ, etc.).
/// Follows Dependency Inversion Principle - domain depends on abstraction, not implementation.
/// </summary>
public interface IEventPublisher
{
    /// <summary>
    /// Publish a banking event for external consumers.
    /// </summary>
    /// <param name="eventMessage">The event message to publish</param>
    Task PublishEventAsync(BankingEventMessage eventMessage);
}