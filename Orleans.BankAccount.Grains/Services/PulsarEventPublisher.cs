using System.Buffers;
using System.Text.Json;
using DotPulsar;
using DotPulsar.Abstractions;
using DotPulsar.Extensions;
using Orleans.BankAccount.Interfaces.Infrastructure;

namespace Orleans.BankAccount.Grains.Services;

/// <summary>
/// Apache Pulsar implementation of IEventPublisher.
/// Publishes domain events to Pulsar topics for external system integration.
/// </summary>
public class PulsarEventPublisher : IEventPublisher
{
    private readonly IPulsarClient _client;
    private readonly IProducer<ReadOnlySequence<byte>> _producer;
    private readonly string _topic;

    public PulsarEventPublisher(string pulsarServiceUrl, string topic = "bank-events")
    {
        _topic = topic;
        
        _client = PulsarClient.Builder()
            .ServiceUrl(new Uri(pulsarServiceUrl))
            .Build();

        _producer = _client.NewProducer()
            .Topic(_topic)
            .Create();
    }

    public async Task PublishEventAsync(BankingEventMessage eventMessage)
    {
        try
        {
            // Serialize event to JSON
            var json = JsonSerializer.Serialize(eventMessage, new JsonSerializerOptions
            {
                WriteIndented = false,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            var messageBytes = System.Text.Encoding.UTF8.GetBytes(json);
            var sequence = new ReadOnlySequence<byte>(messageBytes);
            
            await _producer.Send(sequence);
        }
        catch (Exception ex)
        {
            // Log error but don't fail the grain operation
            Console.WriteLine($"Failed to publish event to Pulsar: {ex.Message}");
            // In production, use proper logging (ILogger)
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _producer.DisposeAsync();
        await _client.DisposeAsync();
    }
}


