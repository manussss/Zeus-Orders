var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.Configure<KafkaSettings>(builder.Configuration.GetSection("Kafka"));
builder.Services.AddSingleton(sp =>
{
    var kafkaSettings = sp.GetRequiredService<IOptions<KafkaSettings>>().Value;

    var config = new ProducerConfig
    {
        BootstrapServers = kafkaSettings.BootstrapServers,
        Acks = Acks.All,            // Wait for all replicas to acknowledge
        EnableIdempotence = true,   // Ensure exactly-once semantics
        MessageSendMaxRetries = 3,  // Retry 3 times
        RetryBackoffMs = 100        // Wait 100ms between retries
    };

    return new ProducerBuilder<Null, string>(config).Build();
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.MapPost("/api/v1/orders", async (
    [FromBody] PlaceOrderRequest placeOrderRequest, 
    CancellationToken cancellationToken,
    ILoggerFactory loggerFactory,
    IProducer<Null, string> producer,
    IOptions<KafkaSettings> kafkaOptions
    ) =>
{
    ILogger logger = loggerFactory.CreateLogger("PlaceOrderEndpoint");
 
    if (placeOrderRequest == null)
    {
        logger.LogError("Invalid request object");
        return Results.BadRequest("The submitted request is not valid or empty");
    }

    await Task.Delay(1000, cancellationToken).ConfigureAwait(false);  // Fake some processing time, charge payment, etc.
    logger.LogInformation("Order processed successfully");

    var orderPlaceEvent = new OrderPlacedEvent
    {
        OrderId = Guid.NewGuid().ToString(),
        UserId = placeOrderRequest.UserId,
        Total = placeOrderRequest.Total,
        Items = [.. placeOrderRequest.Items.Select(i => new OrderItem
        {
            ProductId = i.ProductId,
            Quantity = i.Quantity
        })],
        Timestamp = DateTime.UtcNow,
        PaymentId = Guid.NewGuid().ToString()
    };

    var json = JsonSerializer.Serialize(orderPlaceEvent);

    await producer.ProduceAsync(kafkaOptions.Value.OrderPlacedTopic, new Message<Null, string>
    {
        Value = json
    }).ConfigureAwait(false);

    logger.LogInformation("Order placed event sent to Kafka");

    return Results.Ok();
})
.WithOpenApi();

await app.RunAsync();