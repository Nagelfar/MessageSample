using RabbitMQ.Client;

namespace MessageSample.EventDriven;

public static class Topology
{
    public const string TableServiceTopic = "event-driven-tableservice";
    public const string FoodPreparationTopic = "event-driven-foodprep";

    public const string FoodPreparationSubscription = "event-driven-foodprep";
    public const string DeliverySubscription = "event-driven-delivery";

    public static void DefineTopology(WebApplication app)
    {
        using var channel = app.Services.GetRequiredService<IConnection>().CreateModel();
        channel.ExchangeDeclare(
            exchange: TableServiceTopic,
            ExchangeType.Topic,
            durable: true,
            autoDelete: false
        );

        channel.ExchangeDeclare(
            exchange: FoodPreparationTopic,
            ExchangeType.Topic,
            durable: true,
            autoDelete: false
        );

        channel.QueueDeclare(queue: FoodPreparationSubscription,
            durable: false,
            exclusive: false,
            autoDelete: false,
            arguments: null);
        channel.QueueBind(FoodPreparationSubscription, TableServiceTopic, "#");

        channel.QueueDeclare(queue: DeliverySubscription,
            durable: false,
            exclusive: false,
            autoDelete: false,
            arguments: null);

        channel.QueueBind(DeliverySubscription, TableServiceTopic, "#");
        channel.QueueBind(DeliverySubscription, FoodPreparationTopic, "#");
    }

    public static void Configure(WebApplicationBuilder builder)
    {
        builder.Services.AddHostedService<FoodPreparation>();
        builder.Services.AddHostedService<Delivery>();
    }
}