using RabbitMQ.Client;

namespace MessageSample.CommandDriven;

public static class Topology
{
    public const string FoodPreparationQueue = "commands.cook";
    public const string DeliveryQueue = "commands.delivery";

    public static void DefineTopology(WebApplication app)
    {
        using var channel = app.Services.GetRequiredService<IConnection>().CreateModel();
        var dlqArgs = channel.PrepareDqlFor("commands");
        channel.QueueDeclare(queue: FoodPreparationQueue,
            durable: false,
            exclusive: false,
            autoDelete: false,
            arguments: dlqArgs);

        channel.QueueDeclare(queue: DeliveryQueue,
            durable: false,
            exclusive: false,
            autoDelete: false,
            arguments: dlqArgs);
    }

    public static void Configure(WebApplicationBuilder builder)
    {
        builder.Services.AddHostedService<FoodPreparation>();
        builder.Services.AddHostedService<Delivery>();
    }
}