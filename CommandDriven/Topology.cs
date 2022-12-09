using RabbitMQ.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Builder;

namespace MessageSample.CommandDriven;

public static class Topology
{
    public const string FoodPreparationQueue = "commands-cook";
    public const string DeliveryQueue = "commands-delivery";

    public static void DefineAndRunTopology(WebApplication app)
    {
        using var channel = app.Services.GetRequiredService<IConnection>().CreateModel();
        channel.QueueDeclare(queue: FoodPreparationQueue,
            durable: false,
            exclusive: false,
            autoDelete: false,
            arguments: null);

        channel.QueueDeclare(queue: DeliveryQueue,
            durable: false,
            exclusive: false,
            autoDelete: false,
            arguments: null);

        app.Services.GetRequiredService<FoodPreparation>().Start();
        app.Services.GetRequiredService<Delivery>().Start();
    }

    public static void Configure(WebApplicationBuilder builder)
    {
        builder.Services.AddSingleton<FoodPreparation>();
        builder.Services.AddSingleton<Delivery>();
    }
}