using RabbitMQ.Client;

namespace MessageSample.EventDriven;

public static class Topology
{
    public const string OrdersTopic = "event-driven-orders";

    public const string FoodPreparationSubscription = "event-driven-foodprep";
    public const string DeliverySubscription = "event-driven-delivery";

    public static void DefineTopology(WebApplication app)
    {
        using var channel = app.Services.GetRequiredService<IConnection>().CreateModel();
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
            arguments: null);
        channel.QueueBind(FoodPreparationSubscription, OrdersTopic, "#");

        channel.QueueDeclare(queue: DeliverySubscription,
            durable: false,
            exclusive: false,
            autoDelete: false,
            arguments: null);
        channel.QueueBind(DeliverySubscription, OrdersTopic, "#");
    }

    public static void Configure(WebApplicationBuilder builder)
    {
        builder.Services.AddHostedService<FoodPreparation>();
        builder.Services.AddHostedService<Delivery>();
    }
}