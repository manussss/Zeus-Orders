namespace Zeus.Orders.Inventory.Worker;

public class Worker(
    ILogger<Worker> logger,
    IOptions<KafkaSettings> kafkaOptions) : BackgroundService
{
    private readonly KafkaSettings _kafkaSettings = kafkaOptions.Value;
    private IConsumer<Ignore, string>? _consumer;

    public override Task StartAsync(CancellationToken cancellationToken)
    {
        var config = new ConsumerConfig
        {
            BootstrapServers = _kafkaSettings.BootstrapServers,
            GroupId = _kafkaSettings.GroupId,
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = true
        };

        _consumer = new ConsumerBuilder<Ignore, string>(config).Build();
        _consumer.Subscribe(_kafkaSettings.Topic);

        logger.LogInformation("Kafka consumer started and subscribed to topic: {Topic}", _kafkaSettings.Topic);

        return base.StartAsync(cancellationToken);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Yield(); // Ensures method runs asynchronously

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var result = _consumer?.Consume(stoppingToken);
                if (result == null || string.IsNullOrWhiteSpace(result.Message?.Value))
                    continue;

                OrderPlacedEvent? order;
                try
                {
                    order = JsonSerializer.Deserialize<OrderPlacedEvent>(result.Message.Value);
                }
                catch (JsonException jsonEx)
                {
                    logger.LogError(jsonEx, "Failed to deserialize message: {Message}", result.Message.Value);
                    continue;
                }

                if (order == null)
                {
                    logger.LogWarning("Received null or malformed order event");
                    continue;
                }

                logger.LogInformation("Order received: {OrderId} at {Timestamp}", order.OrderId, order.Timestamp);
                await HandleOrder(order);
            }
            catch (ConsumeException ex)
            {
                logger.LogError(ex, "Kafka consume error: {Reason}", ex.Error.Reason);
            }
            catch (JsonException ex)
            {
                logger.LogError(ex, "Failed to deserialize Kafka message");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Unexpected error processing Kafka message");
            }
        }
    }

    private async Task HandleOrder(OrderPlacedEvent order)
    {
        logger.LogInformation("Order received: {OrderId} at {Timestamp}", order.OrderId, order.Timestamp);

        foreach (var item in order.Items)
        {
            logger.LogInformation(" - Product: {ProductId}, Quantity: {Quantity}", item.ProductId, item.Quantity);
            await Task.Delay(125);
        }

        logger.LogInformation("Order inventory updated: {OrderId}", order.OrderId);
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_consumer != null)
        {
            logger.LogInformation("Closing Kafka consumer...");
            _consumer.Close();
            _consumer.Dispose();
        }

        await base.StopAsync(cancellationToken);
    }
}