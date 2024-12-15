using RabbitMQ.Client;

namespace MessageSample.Saga;

public static class Topology
{
    public const string TableServiceTopic = "saga.tableservice";
    public const string DeliveryTopic = "saga.delivery";
    public const string FoodPreparationTopic = "saga.foodprep";

    public const string FoodPreparationQueue = "saga.foodprep";
    public const string DeliveryQueue = "saga.delivery";

    public const string SagaOrderFulfillment = "saga.orderfulfillment";
    public const string SagaTimeouts = "saga.timeouts";

    public static void DefineTopology(WebApplication app)
    {
        using var channel = app.Services.GetRequiredService<IConnection>().CreateModel();
        var dlqArgs = channel.PrepareDqlFor("saga");
        channel.ExchangeDeclare(
            exchange: TableServiceTopic,
            ExchangeType.Topic,
            durable: true,
            autoDelete: false
        );
        
        channel.ExchangeDeclare(
            exchange: DeliveryTopic,
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

        channel.QueueDeclare(queue: SagaOrderFulfillment,
            durable: false,
            exclusive: false,
            autoDelete: false,
            arguments: dlqArgs);

        channel.ExchangeDeclare(
            exchange: SagaTimeouts,
            type: "x-delayed-message",
            durable: true,
            autoDelete: false,
            arguments: new Dictionary<string, object>
            {
                { "x-delayed-type", "topic" }
            }
        );

        channel.QueueBind(SagaOrderFulfillment, SagaTimeouts, "#");
        channel.QueueBind(SagaOrderFulfillment, DeliveryTopic, "#");
        channel.QueueBind(SagaOrderFulfillment, TableServiceTopic, "#");
        channel.QueueBind(SagaOrderFulfillment, FoodPreparationTopic, "#");
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
                                logger: services.GetLogger<RetryHandler<Envelope>>()
                            ),
                            logger: services.GetLogger<LoggingMessageHandler<Envelope>>()
                        ),
                        logger: services.GetLogger<IdempotencyHandler<Envelope>>()
                    )
                )
            )
        );
    }

    private static void ConfigureSagaFor<TState>(this WebApplicationBuilder builder, string queue,
        Func<IServiceProvider, IEnumerable<IHandleMessage<Envelope>>> handlerFactory) where TState : notnull
    {
        builder.ConfigureFor<TState>(queue,
            services =>
                handlerFactory.Invoke(services).Concat(new[]
                {
                    services.ResolveUpcastEnvelope<TimeoutMessage>()
                })
        );
    }

    private static ILogger<TLogger> GetLogger<TLogger>(this IServiceProvider services)
    {
        return services.GetRequiredService<ILoggerFactory>().CreateLogger<TLogger>();
    }

    public static void Configure(WebApplicationBuilder builder)
    {
        builder.Services.AddTransient<IHandleMessageEnvelope<CookFood>, FoodPreparationHandler>();
        builder.Services.AddTransient<IHandleMessageEnvelope<DeliverDrinks>, DeliveryHandler>();
        builder.Services.AddTransient<IHandleMessageEnvelope<DeliverCookedFood>, DeliveryHandler>();
        
        builder.Services.AddTransient<IHandleMessageEnvelope<OrderPlaced>, OrderFulfillmentSaga>();
        builder.Services.AddTransient<IHandleMessageEnvelope<FoodCooked>, OrderFulfillmentSaga>();
        builder.Services.AddTransient<IHandleMessageEnvelope<ItemsDelivered>, OrderFulfillmentSaga>();
        builder.Services.AddTransient<IHandleMessageEnvelope<TimeoutMessage>, OrderFulfillmentSaga>();
        builder.ConfigureFor<CookFood>(FoodPreparationQueue, services => new[]
        {
            services.ResolveUpcastEnvelope<CookFood>(),
        });
        builder.ConfigureFor<DeliverDrinks>(DeliveryQueue, services => new[]
        {
            services.ResolveUpcastEnvelope<DeliverCookedFood>(),
            services.ResolveUpcastEnvelope<DeliverDrinks>()
        });
        builder.ConfigureSagaFor<OrderFulfillmentState>(Topology.SagaOrderFulfillment,
            services => new[]
            {
                services.ResolveUpcastEnvelope<OrderPlaced>(),
                services.ResolveUpcastEnvelope<FoodCooked>(),
                services.ResolveUpcastEnvelope<ItemsDelivered>()
            });
    }
}