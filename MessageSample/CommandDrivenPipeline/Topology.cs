using RabbitMQ.Client;

namespace MessageSample.CommandDrivenPipeline;

public static class Topology
{
    public const string FoodPreparationQueue = "commands-pipeline.cook";
    public const string DeliveryQueue = "commands-pipeline.delivery";

    public static void DefineTopology(WebApplication app)
    {
        using var channel = app.Services.GetRequiredService<IConnection>().CreateModel();
        channel.BasicQos(0, 1, false);
        var dlqArgs = channel.PrepareDqlFor("commands-pipeline");
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

    private static void ConfigureFor<TMessage>(this WebApplicationBuilder builder, string queue)
        where TMessage : notnull
    {
        builder.Services.AddHostedService(services =>
            new RabbitMqEventHandler<TMessage>(
                services.GetRequiredService<IConnection>(),
                queue,
                new DeserializingHandler<TMessage>(
                    services.ResolveHandler<TMessage>()
                ),
                services.GetLogger<RabbitMqEventHandler<TMessage>>()
            )
        );
    }

    private static IHandleMessage<T> ResolveHandler<T>(this IServiceProvider services) where T : notnull
    {
        return services.GetRequiredService<IHandleMessage<T>>();
    }

    private static IHandleMessage<Envelope> ResolveEnvelopeBody<T>(this IServiceProvider services) where T : notnull
    {
        var handler = services.GetRequiredService<IHandleMessage<T>>();
        return new EnvelopeBodyHandler<T>(handler);
    }
    private static IHandleMessage<Envelope> ResolveUpcastEnvelope<T>(this IServiceProvider services) where T : notnull
    {
        var handler = services.GetRequiredService<IHandleMessageEnvelope<T>>();
        return new UpCastEnvelopeHandler<T>(handler);
    }

    private static void ConfigureFor<TMessage>(this WebApplicationBuilder builder, string queue,
        Func<IServiceProvider, IEnumerable<IHandleMessage<Envelope>>> handlerFactory) where TMessage : notnull
    {
        builder.Services.AddHostedService(services =>
            new RabbitMqEventHandler<TMessage>(
                services.GetRequiredService<IConnection>(),
                queue,
                next: new EnvelopeHandler(
                    new IdempotencyHandler<Envelope>(
                        new LoggingMessageHandler<Envelope>(
                            new RetryHandler<Envelope>(
                                maxRetries: 3,
                                wait: TimeSpan.FromMilliseconds(500),
                                next: new EnvelopeMatchingHandler(
                                    handlerFactory(services)
                                ),
                                logger:  services.GetLogger<RetryHandler<Envelope>>()
                            ),
                            logger:  services.GetLogger<LoggingMessageHandler<Envelope>>()
                            ),
                        logger: services.GetLogger<IdempotencyHandler<Envelope>>()
                    )
                ),
                services.GetLogger<RabbitMqEventHandler<TMessage>>()
            )
        );
    }
    
    private static ILogger<TLogger> GetLogger<TLogger>(this IServiceProvider services)
    {
        return services.GetRequiredService<ILoggerFactory>().CreateLogger<TLogger>();
    }

    public static void Configure(WebApplicationBuilder builder)
    {
        builder.Services.AddTransient<IHandleMessageEnvelope<CookFood>, FoodPreparationHandler>();
        builder.Services.AddTransient<IHandleMessage<DeliverItems>, DeliveryHandler>();
        builder.Services.AddTransient<IHandleMessage<DeliverCookedFood>, DeliveryHandler>();
        builder.ConfigureFor<CookFood>(FoodPreparationQueue,services => new[]
        {
            services.ResolveUpcastEnvelope<CookFood>(),
        });
        builder.ConfigureFor<DeliverItems>(DeliveryQueue, services => new[]
        {
            services.ResolveEnvelopeBody<DeliverCookedFood>(),
            services.ResolveEnvelopeBody<DeliverItems>()
        });
    }
}