using MessageSample.CommandDriven;
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
    {
        builder.Services.AddHostedService(services =>
            new RabbitMqEventHandler<TMessage>(
                services.GetRequiredService<IConnection>(),
                queue,
                new DeserializingHandler<TMessage>(
                    services.GetRequiredService<IHandleMessage<TMessage>>()
                )
            )
        );
    }

    private static IHandleMessage<object> Resolve<T>(this IServiceProvider services)
    {
        var handler = services.GetRequiredService<IHandleMessage<T>>();
        return new UpCastHandler<T>(handler);
    }

    private static void ConfigureFor<TMessage>(this WebApplicationBuilder builder, string queue,
        Func<IServiceProvider, IEnumerable<IHandleMessage<object>>> handlerFactory)
    {
        builder.Services.AddHostedService(services =>
            new RabbitMqEventHandler<TMessage>(
                services.GetRequiredService<IConnection>(),
                queue,
                new EnvelopeHandler(
                    new FanoutHandler(
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
            services.Resolve<DeliverCookedFood>(),
            services.Resolve<DeliverItems>()
        });
    }
}