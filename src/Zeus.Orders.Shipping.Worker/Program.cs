var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddHostedService<Worker>();
builder.Services.Configure<KafkaSettings>(builder.Configuration.GetSection("Kafka"));

var host = builder.Build();
host.Run();
