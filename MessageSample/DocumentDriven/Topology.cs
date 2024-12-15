using RabbitMQ.Client;

namespace MessageSample.DocumentDriven;

public static class Topology
{
    public const string OrdersTopic = "document-driven.orders";

    public const string FoodPreparationSubscription = "document-driven.foodprep";
    public const string DeliverySubscription = "document-driven.delivery";

    public static void DefineTopology(WebApplication app)
    {
        using var channel = app.Services.GetRequiredService<IConnection>().CreateModel();
        var dlqArgs = channel.PrepareDqlFor("document-driven");
        channel.ExchangeDeclare(
            exchange: OrdersTopic,
            ExchangeType.Topic,
            durable: true,
            autoDelete: false
        );

        channel.QueueDeclare(queue: FoodPreparationSubscription,
            durable: false,
            exclusive: false,
            autoDelete: false,
            arguments: dlqArgs);
        channel.QueueBind(FoodPreparationSubscription, OrdersTopic, "#");

        channel.QueueDeclare(queue: DeliverySubscription,
            durable: false,
            exclusive: false,
            autoDelete: false,
            arguments: dlqArgs);

        channel.QueueBind(DeliverySubscription, OrdersTopic, "#");
    }

    public static void Configure(WebApplicationBuilder builder)
    {
        builder.Services.AddHostedService<FoodPreparation>();
        builder.Services.AddHostedService<Delivery>();
    }
}