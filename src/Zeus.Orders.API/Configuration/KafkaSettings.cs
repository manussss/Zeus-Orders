namespace Zeus.Orders.API.Configuration;

public class KafkaSettings
{
    public string? BootstrapServers { get; set; }
    public string? OrderPlacedTopic { get; set; }
}