using RabbitMQ.Client;

namespace MessageSample.CommandDrivenPipeline;

public static class Topology
{
    public const string FoodPreparationQueue = "commands-pipeline-cook";
    public const string DeliveryQueue = "commands-pipeline-delivery";

    public static void DefineTopology(WebApplication app)
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
    }

    private static void ConfigureFor<TMessage>(this WebApplicationBuilder builder, string queue)
        where TMessage : notnull
    {
        builder.Services.AddHostedService(services =>
            new RabbitMqEventHandler<TMessage>(
                services.GetRequiredService<IConnection>(),
                queue,
                new DeserializingHandler<TMessage>(
                    services.ResolveHandler<TMessage>()
                )
            )
        );
    }

    private static IHandleMessage<T> ResolveHandler<T>(this IServiceProvider services) where T : notnull
    {
        return services.GetRequiredService<IHandleMessage<T>>();
    }

    private static IHandleMessage<object> ResolveUpcast<T>(this IServiceProvider services) where T : notnull
    {
        var handler = services.GetRequiredService<IHandleMessage<T>>();
        return new UpCastHandler<T>(handler);
    }

    private static void ConfigureFor<TMessage>(this WebApplicationBuilder builder, string queue,
        Func<IServiceProvider, IEnumerable<IHandleMessage<object>>> handlerFactory) where TMessage : notnull
    {
        builder.Services.AddHostedService(services =>
            new RabbitMqEventHandler<TMessage>(
                services.GetRequiredService<IConnection>(),
                queue,
                new EnvelopeHandler(
                    new TypeMatchingHandler(
                        handlerFactory(services)
                    )
                )
            )
        );
    }

    public static void Configure(WebApplicationBuilder builder)
    {
        builder.Services.AddTransient<IHandleMessage<CookFood>, FoodPreparationHandler>();
        builder.Services.AddTransient<IHandleMessage<DeliverItems>, DeliveryHandler>();
        builder.Services.AddTransient<IHandleMessage<DeliverCookedFood>, DeliveryHandler>();
        builder.ConfigureFor<CookFood>(FoodPreparationQueue);
        builder.ConfigureFor<DeliverItems>(DeliveryQueue, services => new[]
        {
            services.ResolveUpcast<DeliverCookedFood>(),
            services.ResolveUpcast<DeliverItems>()
        });
    }
}